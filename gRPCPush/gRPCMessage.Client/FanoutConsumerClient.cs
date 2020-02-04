using Grpc.Net.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace gRPCMessage.Client
{
    public class FanoutConsumerClient: IDisposable
    {
        private readonly GrpcChannel _grpcChannel;

        private readonly string _address;

        private readonly MessageService.MessageServiceClient _messageServiceClient;

        private string _clientId = Guid.NewGuid().ToString();

        private Action<string> _consumeHook;

        private Task _messageTask;

        private readonly CancellationToken _cancellationToken;

        private bool _healthy;

        private Task<bool> _replayMoveNextTask;

        private Task _pingDelayTask;

        private ReaderWriterLockSlim _healthyLock = new ReaderWriterLockSlim();

        public bool Healthy
        {
            get
            {
                try
                {
                    _healthyLock.EnterReadLock();
                    return _healthy;
                }
                finally
                {
                    _healthyLock.ExitReadLock();

                }
            }
            set
            {
                try
                {
                    _healthyLock.EnterWriteLock();
                    _healthy = value;
                }
                finally
                {
                    _healthyLock.ExitWriteLock();

                }
            }
        }

        public FanoutConsumerClient(string address, Action<string> consumeHook, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _address = address;
            _consumeHook = consumeHook;
            _grpcChannel = GrpcChannel.ForAddress(_address);
            _messageServiceClient = new MessageService.MessageServiceClient(_grpcChannel);
            _messageTask = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await WaitMessageAsync();
                    }
                    catch
                    {
                        // any cases retry
                        Healthy = false;
                    }

                }
            }, _cancellationToken);
        }

        private async Task WaitMessageAsync()
        {
            using (var call = _messageServiceClient.SendMessage(deadline: DateTime.MaxValue, cancellationToken: _cancellationToken))
            {
                await call.RequestStream.WriteAsync(new MessageRequest() { ClientId = _clientId });
                Healthy = true;
                _replayMoveNextTask = call.ResponseStream.MoveNext(_cancellationToken);
                _pingDelayTask = Task.Delay(TimeSpan.FromSeconds(10), _cancellationToken);
                while (true)
                {
                    await Task.WhenAny(_replayMoveNextTask, _pingDelayTask);
                    if (_pingDelayTask.IsCanceled || _replayMoveNextTask.IsCanceled || _replayMoveNextTask.IsFaulted)
                    {
                        Healthy = false;
                        break;
                    }

                    if (_pingDelayTask.IsCompleted)
                    {
                        await call.RequestStream.WriteAsync(new MessageRequest() { ClientId = _clientId });
                        _pingDelayTask = Task.Delay(TimeSpan.FromSeconds(10), _cancellationToken);
                    }

                    if (_replayMoveNextTask.IsCompleted)
                    {
                        var value = call.ResponseStream.Current;
                        _consumeHook?.Invoke(value.Data);
                        _replayMoveNextTask = call.ResponseStream.MoveNext(_cancellationToken);
                    } 
                }
            }
        }

        public void Dispose()
        {
            if (_messageTask != null && !_messageTask.IsCompleted)
            {
                _messageTask.Dispose();
            }

            if (_grpcChannel != null)
            {
                _grpcChannel.Dispose();
            }
        }
    }
}
