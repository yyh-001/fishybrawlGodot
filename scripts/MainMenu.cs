using Godot;
using System;
using System.Threading.Tasks;
using System.Text.Json;

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

        // 连接按钮信号
        _quickMatchButton.Pressed += OnQuickMatchPressed;
        _createRoomButton.Pressed += OnCreateRoomPressed;
        _joinRoomButton.Pressed += OnJoinRoomPressed;
        _logoutButton.Pressed += OnLogoutPressed;
        _addFriendButton.Pressed += OnAddFriendPressed;
        _inviteButton.Pressed += OnInvitePressed;
        _refreshFriendListButton.Pressed += OnRefreshFriendListPressed;

        // 更新用户信息显示
        UpdateUserInfo();

        // 连接 WebSocket
        try 
        {
            await WebSocketManager.Instance.ConnectAsync();
            
            // 禁用按钮直到连接成功
            SetButtonsEnabled(true);
            
            // WebSocket 连接成功后获取好友列表
            await GetFriendList();
        }
        catch (Exception e)
        {
            GD.PrintErr("WebSocket 连接失败:", e.Message);
            ShowError("连接服务器失败，请重试");
            SetButtonsEnabled(false);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        // 断开 WebSocket 连接
        WebSocketManager.Instance.Disconnect();
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

    private void OnQuickMatchPressed()
    {
        if (!WebSocketManager.Instance.IsConnected)
        {
            ShowError("未连接到服务器");
            return;
        }
        GD.Print("快速匹配");
        // TODO: 实现快速匹配逻辑
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
        // 断开 WebSocket 连接
        WebSocketManager.Instance.Disconnect();
        
        // 清除用户信息
        Global.Instance.Token = null;
        Global.Instance.UserInfo = null;

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
        GD.Print("添加好友");
        // TODO: 实现添加好友逻辑
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

                foreach (var friend in friends)
                {
                    var friendData = friend.AsGodotDictionary();
                    string username = (string)friendData["username"];
                    string status = (string)friendData["status"];
                    int rating = (int)friendData["rating"];

                    // 根据状态设置不同的图标或颜色
                    string statusIcon = GetStatusIcon(status);
                    
                    // 添加到列表中
                    _friendList.AddItem($"{statusIcon} {username} ({rating})");
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
} 