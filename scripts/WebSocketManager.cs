using Godot;
using System;
using SocketIOClient;
using SocketIOClient.Transport;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Linq;

/// <summary>
/// WebSocket 管理器，处理与服务器的 Socket.IO 连接
/// </summary>
public class WebSocketManager
{
    private static WebSocketManager _instance;
    /// <summary>
    /// 单例实例
    /// </summary>
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
    /// <summary>
    /// 当前是否已连接到服务器
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// 连接到 Socket.IO 服务器
    /// </summary>
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

    /// <summary>
    /// 断开与服务器的连接
    /// </summary>
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

    /// <summary>
    /// 发送事件到服务器并等待响应
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="data">可选的事件数据</param>
    /// <returns>服务器响应数据</returns>
    public async Task<Godot.Collections.Dictionary> EmitAsync(string eventName, object data = null)
    {
        if (_socket == null)
        {
            throw new InvalidOperationException("Socket is not initialized");
        }

        try
        {
            // 打印请求信息，方便调试
            if (data != null)
            {
                var jsonString = JsonSerializer.Serialize(data);
                GD.Print($"发送 {eventName} 请求: {jsonString}");
            }
            else
            {
                GD.Print($"发送 {eventName} 请求");
            }

            var tcs = new TaskCompletionSource<Godot.Collections.Dictionary>();

            // 根据是否有数据选择不同的发送方式
            if (data != null)
            {
                // 将数据对象转换为字典，以便 Socket.IO 正确序列化
                var jsonElement = JsonSerializer.SerializeToElement(data);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.ToString());
                
                await _socket.EmitAsync(eventName, response =>
                {
                    HandleResponse(response, tcs);
                }, dict);  // 数据作为最后一个参数传递
            }
            else
            {
                await _socket.EmitAsync(eventName, response =>
                {
                    HandleResponse(response, tcs);
                });
            }

            // 添加超时处理
            var timeoutTask = Task.Delay(5000);  // 5秒超时
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

    /// <summary>
    /// 处理服务器响应数据
    /// </summary>
    private void HandleResponse(SocketIOResponse response, TaskCompletionSource<Godot.Collections.Dictionary> tcs)
    {
        try
        {
            var result = new Godot.Collections.Dictionary();
            if (response != null)
            {
                // 打印原始响应数据
                var responseData = response.GetValue<JsonElement>(0);
                GD.Print("===== Socket.IO 响应原始数据 =====");
                GD.Print(responseData.GetRawText());
                GD.Print("================================");

                // 解析响应数据
                var jsonString = responseData.ToString();
                var responseObj = JsonSerializer.Deserialize<ResponseData>(jsonString);
                
                // 转换为 Godot Dictionary
                result["success"] = responseObj.success;
                
                // 处理错误信息
                if (!responseObj.success && !string.IsNullOrEmpty(responseObj.error))
                {
                    result["error"] = responseObj.error;
                    GD.PrintErr($"请求失败: {responseObj.error}");
                }
                
                // 处理响应数据
                if (responseObj.data != null)
                {
                    var data = new Godot.Collections.Dictionary();
                    
                    // 处理刷新商店响应
                    if (responseObj.data.minions != null)
                    {
                        GD.Print("\n解析商店数据:");
                        var minionsArray = new Godot.Collections.Array();
                        foreach (var minion in responseObj.data.minions)
                        {
                            var minionDict = new Godot.Collections.Dictionary
                            {
                                ["_id"] = minion._id,
                                ["name"] = minion.name,
                                ["attack"] = minion.attack,
                                ["health"] = minion.health,
                                ["tier"] = minion.tier,
                                ["tribe"] = minion.tribe,
                                ["abilities"] = new Godot.Collections.Array(
                                    (minion.abilities ?? Array.Empty<string>())
                                    .Select(x => Variant.CreateFrom(x))
                                    .ToArray()
                                ),
                                ["description"] = minion.description
                            };
                            minionsArray.Add(minionDict);
                            
                            GD.Print($"\n随从数据:");
                            GD.Print($"- ID: {minion._id}");
                            GD.Print($"- 名称: {minion.name}");
                            GD.Print($"- 攻击力: {minion.attack}");
                            GD.Print($"- 生命值: {minion.health}");
                            GD.Print($"- 等级: {minion.tier}");
                            GD.Print($"- 种族: {minion.tribe}");
                            GD.Print($"- 描述: {minion.description}");
                        }
                        data["minions"] = minionsArray;
                        
                        if (responseObj.data.remainingCoins.HasValue)
                        {
                            data["remainingCoins"] = responseObj.data.remainingCoins.Value;
                            GD.Print($"\n剩余金币: {responseObj.data.remainingCoins.Value}");
                        }
                    }

                    // 处理英雄选择响应
                    if (responseObj.data.userId != null)
                    {
                        data["userId"] = responseObj.data.userId;
                    }
                    if (responseObj.data.heroId != null)
                    {
                        data["heroId"] = responseObj.data.heroId;
                    }
                    if (responseObj.data.allSelected.HasValue)
                    {
                        data["allSelected"] = responseObj.data.allSelected.Value;
                    }
                    if (!string.IsNullOrEmpty(responseObj.data.message))
                    {
                        data["message"] = responseObj.data.message;
                    }

                    // 处理好友列表
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

                    // 处理英雄列表
                    if (responseObj.data.heroes != null)
                    {
                        var heroes = JsonSerializer.Deserialize<Hero[]>(responseObj.data.heroes.ToString());
                        var heroArray = new Godot.Collections.Array();
                        foreach (var hero in heroes)
                        {
                            var heroDict = new Godot.Collections.Dictionary
                            {
                                ["id"] = hero.id,
                                ["name"] = hero.name,
                                ["description"] = hero.description,
                                ["health"] = hero.health,
                                ["ability"] = new Godot.Collections.Dictionary
                                {
                                    ["name"] = hero.ability_name,
                                    ["description"] = hero.ability_description,
                                    ["type"] = hero.ability_type,
                                    ["cost"] = hero.ability_cost,
                                    ["effect"] = hero.ability_effect
                                }
                            };
                            heroArray.Add(heroDict);
                        }
                        data["heroes"] = heroArray;
                        
                        // 添加选择时间限制
                        if (responseObj.data.selectionTimeLimit.HasValue)
                        {
                            data["selectionTimeLimit"] = responseObj.data.selectionTimeLimit.Value;
                        }
                    }

                    // 处理时间戳
                    if (!string.IsNullOrEmpty(responseObj.data.timestamp))
                    {
                        data["timestamp"] = responseObj.data.timestamp;
                    }

                    result["data"] = data;
                }
            }
            tcs.TrySetResult(result);
        }
        catch (Exception e)
        {
            tcs.TrySetException(e);
            GD.PrintErr($"解析响应失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    // 响应数据类型定义
    private class ResponseData
    {
        public bool success { get; set; }
        public ResponseDataContent data { get; set; }
        public string error { get; set; }
    }

    private class ResponseDataContent
    {
        public Minion[] minions { get; set; }
        public int? remainingCoins { get; set; }
        public Friend[] friends { get; set; }
        public object heroes { get; set; }
        public string message { get; set; }
        public string timestamp { get; set; }
        public int? selectionTimeLimit { get; set; }
        
        // 添加英雄选择响应字段
        public string userId { get; set; }
        public string heroId { get; set; }
        public bool? allSelected { get; set; }
    }

    private class Friend
    {
        public string userId { get; set; }
        public string username { get; set; }
        public int rating { get; set; }
        public string status { get; set; }
        public string lastOnline { get; set; }
    }

    private class Hero
    {
        [JsonPropertyName("_id")]
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int health { get; set; }
        public string ability_name { get; set; }
        public string ability_description { get; set; }
        public string ability_type { get; set; }
        public int ability_cost { get; set; }
        public string ability_effect { get; set; }
    }

    private class Minion
    {
        public string _id { get; set; }
        public string name { get; set; }
        public int attack { get; set; }
        public int health { get; set; }
        public int tier { get; set; }
        public string tribe { get; set; }
        public string[] abilities { get; set; }
        public string description { get; set; }
    }
} 