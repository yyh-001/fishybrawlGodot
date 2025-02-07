using Godot;
using System;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using SocketIOClient;

public partial class MainMenu : Control
{
    private Label _usernameLabel;
    private Label _ratingLabel;
    private Button _quickMatchButton;
    private Button _createRoomButton;
    private Button _joinRoomButton;
    private Button _logoutButton;
    private ItemList _friendList;
    private Button _addFriendButton;
    private Button _inviteButton;
    private Button _refreshFriendListButton;
    private PopupMenu _friendPopupMenu;
    private Dictionary<int, string> _friendIdMap = new Dictionary<int, string>();  // 用于存储列表索引到好友ID的映射
    private Window _addFriendDialog;
    private LineEdit _userIdInput;
    private TextEdit _messageInput;
    private Button _sendFriendRequestButton;
    private Button _cancelFriendRequestButton;
    private Label _myUserIdLabel;
    private Button _copyUserIdButton;
    private ItemList _requestList;
    private Dictionary<int, string> _requestIdMap = new Dictionary<int, string>();  // 存储请求ID映射
    private Button _acceptRequestButton;
    private Button _rejectRequestButton;

    public override async void _Ready()
    {
        // 获取节点引用
        _usernameLabel = GetNode<Label>("HBoxContainer/LeftPanel/UserInfo/Username");
        _ratingLabel = GetNode<Label>("HBoxContainer/LeftPanel/UserInfo/Rating");
        _quickMatchButton = GetNode<Button>("HBoxContainer/LeftPanel/ButtonContainer/QuickMatchButton");
        _createRoomButton = GetNode<Button>("HBoxContainer/LeftPanel/ButtonContainer/CreateRoomButton");
        _joinRoomButton = GetNode<Button>("HBoxContainer/LeftPanel/ButtonContainer/JoinRoomButton");
        _logoutButton = GetNode<Button>("HBoxContainer/LeftPanel/ButtonContainer/LogoutButton");
        _friendList = GetNode<ItemList>("HBoxContainer/RightPanel/TabContainer/好友/FriendList");
        _addFriendButton = GetNode<Button>("HBoxContainer/RightPanel/TabContainer/好友/ButtonContainer/AddFriendButton");
        _inviteButton = GetNode<Button>("HBoxContainer/RightPanel/TabContainer/好友/ButtonContainer/InviteButton");
        _refreshFriendListButton = GetNode<Button>("HBoxContainer/RightPanel/TabContainer/好友/ButtonContainer/RefreshButton");

        // 获取右键菜单引用
        _friendPopupMenu = GetNode<PopupMenu>("HBoxContainer/RightPanel/TabContainer/好友/FriendList/PopupMenu");

        // 获取添加好友对话框的引用
        _addFriendDialog = GetNode<Window>("AddFriendDialog");
        _userIdInput = GetNode<LineEdit>("AddFriendDialog/VBoxContainer/UserIdContainer/UserIdInput");
        _messageInput = GetNode<TextEdit>("AddFriendDialog/VBoxContainer/MessageContainer/MessageInput");
        _sendFriendRequestButton = GetNode<Button>("AddFriendDialog/VBoxContainer/ButtonContainer/SendButton");
        _cancelFriendRequestButton = GetNode<Button>("AddFriendDialog/VBoxContainer/ButtonContainer/CancelButton");

        // 获取用户ID显示相关节点
        _myUserIdLabel = GetNode<Label>("AddFriendDialog/VBoxContainer/MyUserIdContainer/MyUserIdLabel");
        _copyUserIdButton = GetNode<Button>("AddFriendDialog/VBoxContainer/MyUserIdContainer/CopyButton");

        // 获取好友请求列表引用
        _requestList = GetNode<ItemList>("HBoxContainer/RightPanel/TabContainer/好友请求/RequestList");

        // 获取好友请求按钮引用
        _acceptRequestButton = GetNode<Button>("HBoxContainer/RightPanel/TabContainer/好友请求/ButtonContainer/AcceptButton");
        _rejectRequestButton = GetNode<Button>("HBoxContainer/RightPanel/TabContainer/好友请求/ButtonContainer/RejectButton");

        // 连接按钮信号
        _quickMatchButton.Pressed += OnQuickMatchPressed;
        _createRoomButton.Pressed += OnCreateRoomPressed;
        _joinRoomButton.Pressed += OnJoinRoomPressed;
        _logoutButton.Pressed += OnLogoutPressed;
        _addFriendButton.Pressed += OnAddFriendPressed;
        _inviteButton.Pressed += OnInvitePressed;
        _refreshFriendListButton.Pressed += OnRefreshFriendListPressed;

        // 连接右键菜单信号
        _friendList.GuiInput += OnFriendListGuiInput;
        _friendPopupMenu.IdPressed += OnFriendPopupMenuItemSelected;

        // 连接对话框按钮信号
        _sendFriendRequestButton.Pressed += OnSendFriendRequestPressed;
        _cancelFriendRequestButton.Pressed += OnCancelFriendRequestPressed;
        _addFriendDialog.CloseRequested += OnCancelFriendRequestPressed;

        // 连接复制按钮信号
        _copyUserIdButton.Pressed += OnCopyUserIdPressed;

        // 连接好友请求按钮信号
        _acceptRequestButton.Pressed += OnAcceptRequestPressed;
        _rejectRequestButton.Pressed += OnRejectRequestPressed;

        // 连接 WebSocket
        try 
        {
            // 确保 WebSocket 已连接
            await Global.Instance.InitializeWebSocket();

        // 更新用户信息显示
        UpdateUserInfo();
            
            // 注册事件监听
            GD.Print("开始注册 Socket.IO 事件监听器...");
            
            WebSocketManager.Instance.Socket.On("friendRequestReceived", response =>
            {
                GD.Print("触发好友请求事件监听器");
                Callable.From(() => OnFriendRequestReceived(response)).CallDeferred();
            });

            WebSocketManager.Instance.Socket.On("friendRequestHandled", response =>
            {
                GD.Print("触发好友请求处理结果事件监听器");
                Callable.From(() => OnFriendRequestHandled(response)).CallDeferred();
            });
            
            WebSocketManager.Instance.Socket.On("matchFound", response =>
            {
                GD.Print("触发匹配成功事件监听器");
                Callable.From(() => OnMatchFound(response)).CallDeferred();
            });
            
            GD.Print("Socket.IO 事件监听器注册完成");
            
            SetButtonsEnabled(true);
            await GetFriendList();
        }
        catch (Exception e)
        {
            GD.PrintErr("初始化失败:", e.Message);
            ShowError("连接服务器失败，请重试");
            SetButtonsEnabled(false);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        
        // 只取消注册当前场景的事件监听器
        if (WebSocketManager.Instance.Socket != null)
        {
            GD.Print("取消注册主菜单的 Socket.IO 事件监听器");
            WebSocketManager.Instance.Socket.Off("friendRequestReceived");
            WebSocketManager.Instance.Socket.Off("friendRequestHandled");
            WebSocketManager.Instance.Socket.Off("matchFound");
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _quickMatchButton.Disabled = !enabled;
        _createRoomButton.Disabled = !enabled;
        _joinRoomButton.Disabled = !enabled;
        _addFriendButton.Disabled = !enabled;
        _inviteButton.Disabled = !enabled;
    }

    private void ShowError(string message)
    {
        // TODO: 实现错误提示UI
        GD.PrintErr(message);
        OS.Alert(message, "错误");
    }

    private void UpdateUserInfo()
    {
        if (Global.Instance.UserInfo != null)
        {
            _usernameLabel.Text = $"用户名: {Global.Instance.UserInfo.username}";
            _ratingLabel.Text = $"积分: {Global.Instance.UserInfo.rating}";
        }
    }

    private async void OnQuickMatchPressed()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }

        try
        {
            _quickMatchButton.Text = "匹配中...";
            _quickMatchButton.Disabled = true;

            var response = await WebSocketManager.Instance.EmitAsync("startMatching");
            GD.Print("匹配响应数据:", response.ToString());
            
            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                if (response.TryGetValue("data", out var dataValue))
                {
                    var data = dataValue.AsGodotDictionary();
                    
                    GD.Print($"===== 开始匹配 =====");
                    if (data.ContainsKey("userId"))
                        GD.Print($"用户ID: {data["userId"].AsString()}");
                    if (data.ContainsKey("rating"))
                        GD.Print($"积分: {data["rating"].AsInt32()}");
                    if (data.ContainsKey("status"))
                        GD.Print($"状态: {data["status"].AsString()}");
                    if (data.ContainsKey("startTime"))
                        GD.Print($"开始时间: {data["startTime"].AsString()}");
                    GD.Print("===================");
                }
                else
                {
                    GD.PrintErr("匹配响应中缺少 data 字段");
                }
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : "开始匹配失败";
                ShowError(errorMessage);
                ResetMatchButton();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"开始匹配失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
            ShowError("开始匹配失败");
            ResetMatchButton();
        }
    }

    private void ResetMatchButton()
    {
        _quickMatchButton.Text = "快速匹配";
        _quickMatchButton.Disabled = false;
    }

    private async void OnMatchFound(SocketIOClient.SocketIOResponse response)
    {
        try
        {
            GD.Print("收到匹配成功数据:", response.GetValue<object>().ToString());
            
            var data = response.GetValue<JsonElement>(0);
            var success = data.GetProperty("success").GetBoolean();
            var roomId = data.GetProperty("roomId").GetString();

            GD.Print($"===== 匹配成功 =====");
            GD.Print($"房间ID: {roomId}");
            GD.Print("===================");

            // 加入房间
            var joinRoomData = new Dictionary<string, string>
            {
                ["roomId"] = roomId
            };

            GD.Print("正在加入房间...");
            var joinResponse = await WebSocketManager.Instance.EmitAsync("joinRoom", joinRoomData);

            if (joinResponse.TryGetValue("success", out var joinSuccess) && (bool)joinSuccess)
            {
                var joinData = joinResponse["data"].AsGodotDictionary();
                
                GD.Print($"===== 加入房间成功 =====");
                
                // 检查并打印房间信息
                if (joinData.ContainsKey("roomId"))
                    GD.Print($"房间ID: {joinData["roomId"]}");
                if (joinData.ContainsKey("name"))
                    GD.Print($"房间名称: {joinData["name"]}");
                if (joinData.ContainsKey("maxPlayers"))
                    GD.Print($"最大玩家数: {joinData["maxPlayers"]}");
                if (joinData.ContainsKey("status"))
                    GD.Print($"房间状态: {joinData["status"]}");
                
                // 检查并打印玩家列表
                if (joinData.ContainsKey("players"))
                {
                    var players = joinData["players"].AsGodotArray();
                    GD.Print("\n当前玩家列表:");
                    foreach (var player in players)
                    {
                        var playerData = player.AsGodotDictionary();
                        string username = playerData.ContainsKey("username") ? playerData["username"].AsString() : "未知";
                        bool ready = playerData.ContainsKey("ready") ? playerData["ready"].AsBool() : false;
                        bool isCreator = playerData.ContainsKey("isCreator") ? playerData["isCreator"].AsBool() : false;
                        
                        GD.Print($"- {username} (准备状态: {ready}, 房主: {isCreator})");
                    }
                }
                
                GD.Print("=======================");

                // 存储房间ID
                Global.Instance.CurrentRoomId = roomId;

                // 切换到游戏场景
                GetTree().ChangeSceneToFile("res://scenes/game.tscn");
            }
            else
            {
                string errorMessage = joinResponse.ContainsKey("error") ? 
                    (string)joinResponse["error"] : "加入房间失败";
                throw new Exception(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"处理匹配成功失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
            ResetMatchButton();
        }
    }

    private void OnCreateRoomPressed()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }
        GD.Print("创建房间");
        // TODO: 实现创建房间逻辑
    }

    private void OnJoinRoomPressed()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }
        GD.Print("加入房间");
        // TODO: 实现加入房间逻辑
    }

    private void OnLogoutPressed()
    {
        // 清理所有数据
        Global.Instance.Cleanup();

        // 返回登录界面
        GetTree().ChangeSceneToFile("res://scenes/login.tscn");
    }

    private void OnAddFriendPressed()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }
        
        // 清空输入框
        _userIdInput.Text = "";
        _messageInput.Text = "";
        
        // 设置当前用户ID
        if (Global.Instance.UserInfo != null)
        {
            _myUserIdLabel.Text = Global.Instance.UserInfo.userId;
        }
        
        // 显示对话框
        _addFriendDialog.Popup();
    }

    private void OnInvitePressed()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }
        GD.Print("邀请组队");
        // TODO: 实现邀请组队逻辑
    }

    private async Task GetFriendList()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }

        try
        {
            // 清空当前列表
            _friendList.Clear();

            // 发送获取好友列表请求
            var responseData = await WebSocketManager.Instance.EmitAsync("getFriends");
            
            GD.Print("获取好友列表响应:", responseData);

            if (responseData.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                var data = responseData["data"].AsGodotDictionary();
                var friends = data["friends"].AsGodotArray();

                _friendIdMap.Clear();  // 清空映射
                int index = 0;
                foreach (var friend in friends)
                {
                    var friendData = friend.AsGodotDictionary();
                    string userId = (string)friendData["userId"];
                    string username = (string)friendData["username"];
                    string status = (string)friendData["status"];
                    int rating = (int)friendData["rating"];

                    // 根据状态设置不同的图标或颜色
                    string statusIcon = GetStatusIcon(status);
                    
                    // 添加到列表中
                    _friendList.AddItem($"{statusIcon} {username} ({rating})");
                    _friendIdMap[index] = userId;  // 保存映射
                    index++;
                }
            }
            else
            {
                string errorMessage = responseData.ContainsKey("error") ? 
                    (string)responseData["error"] : "获取好友列表失败";
                ShowError(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("获取好友列表失败:", e.Message);
            ShowError("获取好友列表失败");
        }
    }

    private string GetStatusIcon(string status)
    {
        return status switch
        {
            "online" => "🟢",    // 在线
            "offline" => "⚫",   // 离线
            "in_game" => "🎮",   // 游戏中
            _ => "❓"            // 未知状态
        };
    }

    private async void OnRefreshFriendListPressed()
    {
        await GetFriendList();
    }

    private void OnFriendListGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && 
            mouseButton.ButtonIndex == MouseButton.Right && 
            mouseButton.Pressed)
        {
            var index = _friendList.GetItemAtPosition(mouseButton.Position);
            if (index != -1)
            {
                _friendList.Select(index);
                _friendPopupMenu.Position = (Vector2I)GetViewport().GetMousePosition();
                _friendPopupMenu.Show();
            }
        }
    }

    private async void OnFriendPopupMenuItemSelected(long id)
    {
        if (id == 0)  // 删除好友
        {
            int selectedIdx = _friendList.GetSelectedItems()[0];
            if (_friendIdMap.TryGetValue(selectedIdx, out string friendId))
            {
                await RemoveFriend(friendId);
            }
        }
    }

    private async Task RemoveFriend(string friendId)
    {
        try
        {
            // 使用字典而不是匿名对象
            var requestData = new Dictionary<string, string>
            {
                ["friendId"] = friendId
            };

            var response = await WebSocketManager.Instance.EmitAsync("removeFriend", requestData);
            
            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                GD.Print("好友删除成功");
                await GetFriendList();
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : "删除好友失败";
                ShowError(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("删除好友失败:", e.Message);
            ShowError("删除好友失败");
        }
    }

    private async void OnSendFriendRequestPressed()
    {
        var userId = _userIdInput.Text.Trim();
        if (string.IsNullOrEmpty(userId))
        {
            ShowError("请输入用户ID");
            return;
        }

        try
        {
            var requestData = new Dictionary<string, string>
            {
                ["toUserId"] = userId
            };

            var message = _messageInput.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                requestData["message"] = message;
            }

            var response = await WebSocketManager.Instance.EmitAsync("sendFriendRequest", requestData);
            
            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                GD.Print("好友请求发送成功");
                _addFriendDialog.Hide();
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : "发送好友请求失败";
                ShowError(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("发送好友请求失败:", e.Message);
            ShowError("发送好友请求失败");
        }
    }

    private void OnCancelFriendRequestPressed()
    {
        _addFriendDialog.Hide();
    }

    private void OnCopyUserIdPressed()
    {
        if (Global.Instance.UserInfo != null)
        {
            DisplayServer.ClipboardSet(Global.Instance.UserInfo.userId);
            GD.Print("用户ID已复制到剪贴板");
        }
    }

    private void OnFriendRequestReceived(SocketIOClient.SocketIOResponse response)
    {
        try
        {
            GD.Print("收到好友请求原始数据:", response.GetValue<object>().ToString());
            
            var data = response.GetValue<JsonElement>(0);
            var requestId = data.GetProperty("requestId").GetString();
            var fromUser = data.GetProperty("fromUser");
            var userId = fromUser.GetProperty("userId").GetString();
            var username = fromUser.GetProperty("username").GetString();
            // message 是可选字段
            string message = "请求添加您为好友";  // 默认消息
            if (data.TryGetProperty("message", out var messageElement))
            {
                message = messageElement.GetString();
            }
            var timestamp = data.GetProperty("timestamp").GetString();

            GD.Print($"===== 收到好友请求 =====");
            GD.Print($"请求ID: {requestId}");
            GD.Print($"发送者: {username} (ID: {userId})");
            GD.Print($"消息内容: {message}");
            GD.Print($"发送时间: {timestamp}");
            GD.Print("======================");

            // 添加到请求列表
            var index = _requestList.AddItem($"来自 {username} 的好友请求: {message}");
            _requestIdMap[index] = requestId;
        }
        catch (Exception e)
        {
            GD.PrintErr($"处理好友请求失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    private void OnFriendRequestHandled(SocketIOClient.SocketIOResponse response)
    {
        try
        {
            GD.Print("收到好友请求处理结果原始数据:", response.GetValue<object>().ToString());
            
            var data = response.GetValue<JsonElement>();
            var requestId = data.GetProperty("requestId").GetString();
            var status = data.GetProperty("status").GetString();
            var toUser = data.GetProperty("toUser");
            var username = toUser.GetProperty("username").GetString();

            GD.Print($"===== 好友请求处理结果 =====");
            GD.Print($"请求ID: {requestId}");
            GD.Print($"处理者: {username}");
            GD.Print($"处理结果: {(status == "accepted" ? "接受" : "拒绝")}");
            GD.Print("===========================");

            // 如果请求被接受，刷新好友列表
            if (status == "accepted")
            {
                GD.Print("好友请求已接受，正在刷新好友列表...");
                Callable.From(GetFriendList).CallDeferred();
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"处理好友请求结果失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    private async Task HandleFriendRequest(string requestId, bool accept)
    {
        try
        {
            var requestData = new Dictionary<string, string>
            {
                ["requestId"] = requestId,
                ["action"] = accept ? "accept" : "reject"  // 使用 action 字段，值为 accept 或 reject
            };

            var response = await WebSocketManager.Instance.EmitAsync("handleFriendRequest", requestData);
            
            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                var data = response["data"].AsGodotDictionary();
                var message = data["message"].AsString();
                GD.Print($"处理好友请求成功: {message}");
                
                // 从列表中移除该请求
                for (int i = 0; i < _requestList.ItemCount; i++)
                {
                    if (_requestIdMap.TryGetValue(i, out var id) && id == requestId)
                    {
                        _requestList.RemoveItem(i);
                        _requestIdMap.Remove(i);
                        break;
                    }
                }

                // 如果接受了请求，刷新好友列表
                if (accept)
                {
                    await GetFriendList();
                }
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : $"处理好友请求失败";
                ShowError(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("处理好友请求失败:", e.Message);
            ShowError("处理好友请求失败");
        }
    }

    private async void OnAcceptRequestPressed()
    {
        var selectedItems = _requestList.GetSelectedItems();
        if (selectedItems.Length == 0)
        {
            ShowError("请先选择要处理的好友请求");
            return;
        }

        var index = selectedItems[0];
        if (_requestIdMap.TryGetValue(index, out string requestId))
        {
            await HandleFriendRequest(requestId, true);
        }
    }

    private async void OnRejectRequestPressed()
    {
        var selectedItems = _requestList.GetSelectedItems();
        if (selectedItems.Length == 0)
        {
            ShowError("请先选择要处理的好友请求");
            return;
        }

        var index = selectedItems[0];
        if (_requestIdMap.TryGetValue(index, out string requestId))
        {
            await HandleFriendRequest(requestId, false);
        }
    }
} 