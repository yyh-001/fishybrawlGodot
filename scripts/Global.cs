using Godot;
using System;
using System.Threading.Tasks;
using SocketIOClient;
using System.Collections.Generic;

public class Global
{
    private static Global _instance;
    public static Global Instance
    {
        get
        {
            if (_instance == null)
                _instance = new Global();
            return _instance;
        }
    }

    public string Token { get; set; }
    public UserInfo UserInfo { get; set; }
    public string CurrentRoomId { get; set; }
    public UserGameInfo CurrentGameInfo { get; set;}  // 添加游戏中的用户信息

    // WebSocket 连接状态
    private bool _isWebSocketInitialized = false;

    /// <summary>
    /// 初始化 WebSocket 连接
    /// </summary>
    public async Task InitializeWebSocket()
    {
        if (!_isWebSocketInitialized)
        {
            try
            {
                await WebSocketManager.Instance.ConnectAsync();
                _isWebSocketInitialized = true;
                GD.Print("WebSocket 初始化成功");
            }
            catch (Exception e)
            {
                GD.PrintErr("WebSocket 初始化失败:", e.Message);
                throw;
            }
        }
    }

    /// <summary>
    /// 清理 WebSocket 连接
    /// </summary>
    public void CleanupWebSocket()
    {
        if (_isWebSocketInitialized)
        {
            WebSocketManager.Instance.Disconnect();
            _isWebSocketInitialized = false;
            GD.Print("WebSocket 连接已清理");
        }
    }

    /// <summary>
    /// 清理用户数据
    /// </summary>
    public void Cleanup()
    {
        Token = null;
        UserInfo = null;
        CurrentRoomId = null;
        CurrentGameInfo = null;  // 清理游戏信息
        CleanupWebSocket();
        GD.Print("Global 数据已清理");
    }
}

// 游戏中的用户信息
public class UserGameInfo
{
    public string UserId { get; set; }
    public string Username { get; set; }
    public string HeroId { get; set; }
    public bool IsBot { get; set; }
    public int Health { get; set; }
    public int Coins { get; set; }
    public int TavernTier { get; set; }
    public HeroInfo Hero { get; set; }
    public List<object> Board { get; set; } = new List<object>();
    public List<object> Hand { get; set; } = new List<object>();
} 