using Grpc.Core;
using System;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Collections.Concurrent;
using System.Threading;
using System.Collections.Generic;

namespace gRPCMessage.Service
{
    public sealed class FanoutMessageService:MessageService.MessageServiceBase
    {
        private Channel<MessageReply> _channel;

        private static readonly ConcurrentDictionary<string, FanoutMessageService> _keepAlivedServices = new ConcurrentDictionary<string, FanoutMessageService>();

        private DateTime _lastPingTime;

        private Task<bool> _pingTask;

        private Task<MessageReply> _channelTask;

        private string _connectionId = Guid.NewGuid().ToString();

        private static readonly TimeSpan _idleTime = TimeSpan.FromMinutes(30);

        public FanoutMessageService()
        {
            _channel = Channel.CreateUnbounded<MessageReply>();
            _lastPingTime = DateTime.UtcNow;
            _keepAlivedServices.GetOrAdd(_connectionId, this);
        }

        public static async Task FanoutAsync(MessageReply message)
        {
            var now = DateTime.UtcNow;
            var removedKeys = new List<string>();
            foreach (var oneServicePair in _keepAlivedServices)
            {
                if ((now - oneServicePair.Value._lastPingTime) < _idleTime)
                {
                    await oneServicePair.Value._channel.Writer.WriteAsync(message);
                }
                else
                {
                    removedKeys.Add(oneServicePair.Key);
                }
            }

            foreach (var oneKey in removedKeys)
            {
                _keepAlivedServices.TryRemove(oneKey, out var service);
            }


        }

        public override async Task SendMessage(IAsyncStreamReader<MessageRequest> requestStream, IServerStreamWriter<MessageReply> responseStream, ServerCallContext context)
        {
            while (true)
            {
                try
                {
                    // remove useless connections
                    if (!_keepAlivedServices.ContainsKey(_connectionId))
                    {
                        break;
                    }

                    if (_pingTask == null)
                    {
                        _pingTask = requestStream.MoveNext(context.CancellationToken);
                    }

                    if (_channelTask == null)
                    {
                        _channelTask = _channel.Reader.ReadAsync(context.CancellationToken).AsTask();
                    }

                    var delayTask = Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);

                    await Task.WhenAny(_pingTask, _channelTask, delayTask);
                    if ((_pingTask.IsFaulted || _channelTask.IsFaulted) || (_pingTask.IsCanceled || _pingTask.IsCanceled) || delayTask.IsCanceled)
                    {
                        _keepAlivedServices.TryRemove(_connectionId, out var value);
                        // any cases close connection
                        break;
                    }

                    if (_pingTask.IsCompletedSuccessfully)
                    {
                        _lastPingTime = DateTime.UtcNow;
                        _pingTask = requestStream.MoveNext(context.CancellationToken);
                    }

                    if (_channelTask.IsCompletedSuccessfully)
                    {
                        await responseStream.WriteAsync(_channelTask.Result);
                        _channelTask = _channel.Reader.ReadAsync(context.CancellationToken).AsTask();
                    }
                }
                catch
                {
                    _keepAlivedServices.TryRemove(_connectionId, out var value);
                    // any cases close connection
                    break;
                }             
            }
        }
    }
}
