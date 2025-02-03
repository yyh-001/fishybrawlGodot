using Godot;
using System;
using SocketIOClient;
using SocketIOClient.Transport;
using System.Threading.Tasks;
using System.Text.Json;

public class WebSocketManager
{
    private static WebSocketManager _instance;
    public static WebSocketManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new WebSocketManager();
            return _instance;
        }
    }

    private SocketIOClient.SocketIO _socket;
    public bool IsConnected { get; private set; }

    public async Task ConnectAsync()
    {
        try
        {
            _socket = new SocketIOClient.SocketIO("http://localhost:3000", new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
                Auth = new { token = Global.Instance.Token },
                Reconnection = true,
                ReconnectionAttempts = 5,
                Path = "/socket.io"  // Socket.IO 路径
            });

            // 连接事件处理
            _socket.OnConnected += (sender, e) =>
            {
                IsConnected = true;
                GD.Print("WebSocket 连接成功");
            };

            _socket.OnDisconnected += (sender, e) =>
            {
                IsConnected = false;
                GD.Print("WebSocket 断开连接:", e);
            };

            // 监听重连事件
            _socket.On("reconnect_attempt", response =>
            {
                GD.Print("正在重新连接..., 尝试次数:", response);
            });

            _socket.On("reconnect", response =>
            {
                IsConnected = true;
                GD.Print("重新连接成功");
            });

            _socket.OnError += (sender, e) =>
            {
                GD.PrintErr("WebSocket 错误:", e);
            };

            await _socket.ConnectAsync();
        }
        catch (Exception e)
        {
            GD.PrintErr("WebSocket 连接失败:", e.Message);
            throw;
        }
    }

    public void Disconnect()
    {
        if (_socket != null)
        {
            _socket.Dispose();
            _socket = null;
            IsConnected = false;
        }
    }

    public SocketIOClient.SocketIO Socket => _socket;

    public async Task<Godot.Collections.Dictionary> EmitAsync(string eventName)
    {
        if (_socket == null)
        {
            throw new InvalidOperationException("Socket is not initialized");
        }

        try
        {
            var tcs = new TaskCompletionSource<Godot.Collections.Dictionary>();

            await _socket.EmitAsync(eventName, response =>
            {
                try
                {
                    var result = new Godot.Collections.Dictionary();
                    if (response != null)
                    {
                        // 打印原始响应数据
                        GD.Print($"Raw response: {response}");
                        
                        // 获取响应数据
                        var responseData = response.GetValue<JsonElement>(0);
                        var jsonString = responseData.ToString();
                        GD.Print($"Response JSON: {jsonString}");

                        // 解析响应
                        var responseObj = JsonSerializer.Deserialize<ResponseData>(jsonString);
                        
                        // 转换为 Godot Dictionary
                        result["success"] = responseObj.success;
                        if (responseObj.data != null)
                        {
                            var data = new Godot.Collections.Dictionary();
                            if (responseObj.data.friends != null)
                            {
                                var friends = new Godot.Collections.Array();
                                foreach (var friend in responseObj.data.friends)
                                {
                                    var friendDict = new Godot.Collections.Dictionary
                                    {
                                        ["userId"] = friend.userId,
                                        ["username"] = friend.username,
                                        ["rating"] = friend.rating,
                                        ["status"] = friend.status,
                                        ["lastOnline"] = friend.lastOnline
                                    };
                                    friends.Add(friendDict);
                                }
                                data["friends"] = friends;
                            }
                            result["data"] = data;
                        }
                    }
                    tcs.TrySetResult(result);
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                    GD.PrintErr($"Parse response failed: {e.Message}");
                }
            });

            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("Request timeout");
            }

            return await tcs.Task;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Emit {eventName} failed:", e.Message);
            throw;
        }
    }

    // 响应数据类型定义
    private class ResponseData
    {
        public bool success { get; set; }
        public ResponseDataContent data { get; set; }
    }

    private class ResponseDataContent
    {
        public Friend[] friends { get; set; }
    }

    private class Friend
    {
        public string userId { get; set; }
        public string username { get; set; }
        public int rating { get; set; }
        public string status { get; set; }
        public string lastOnline { get; set; }
    }
} 