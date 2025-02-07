using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

public partial class Game : Control
{
    private Window _heroSelectDialog;
    private Button[] _heroButtons;
    private Label _timerLabel;  // 添加倒计时标签
    private double _remainingTime;
    private bool _isCountingDown;
    private Dictionary<string, string> _heroIdMap = new Dictionary<string, string>();  // 添加到类成员
    private string _gameId;  // 添加游戏ID
    private string _roomId;  // 添加房间ID
    private int _currentTurn;  // 当前回合
    private string _currentPhase;  // 当前阶段
    private DateTime _startTime;  // 游戏开始时间
    private List<PlayerInfo> _players;  // 玩家信息列表

    // 商店区域
    private Label _coinsLabel;
    private Label _tavernTierLabel;
    private Button _menuButton;
    private Control _shopCardsContainer;
    private Button _upgradeTavernButton;
    private Button _refreshShopButton;
    private Button _freezeShopButton;
    
    // 战场区域
    private Control _playersListContainer;
    private Control _battlefieldContainer;
    private Control _upperBattlefield;
    private Control _lowerBattlefield;
    
    // 手牌区域
    private Control _handCardsContainer;
    private TextureRect _playerAvatar;
    private Label _playerHealthLabel;

    private List<Card> _shopCards = new List<Card>();
    private PackedScene _cardScene;
    private List<Card> _handCards = new List<Card>();  // 添加手牌列表

    // 修改卡牌尺寸常量
    private const float CARD_WIDTH = 80f;    // 从 100f 改为 80f
    private const float CARD_HEIGHT = 120f;  // 从 140f 改为 120f

    public override async void _Ready()
    {
        try 
        {
            // 获取节点引用
            _heroSelectDialog = GetNode<Window>("HeroSelectDialog");
            _timerLabel = GetNode<Label>("HeroSelectDialog/MarginContainer/VBoxContainer/TimerLabel");
            
            // 获取英雄按钮
            _heroButtons = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                var button = GetNode<Button>($"HeroSelectDialog/MarginContainer/VBoxContainer/HeroGrid/Hero{i + 1}");
                _heroButtons[i] = button;
                
                // 使用按钮本身作为参数，而不是其初始文本
                int index = i;  // 捕获索引
                button.Pressed += () => 
                {
                    if (!string.IsNullOrEmpty(_heroButtons[index].Text))
                    {
                        OnHeroSelected(_heroButtons[index].Text);
                    }
                };
            }

            // 注册游戏相关的事件监听器
            if (WebSocketManager.Instance.Socket != null)
            {
                GD.Print("注册游戏场景的 Socket.IO 事件监听器");
                WebSocketManager.Instance.Socket.On("gameStart", response =>
                {
                    GD.Print("触发游戏开始监听器");
                    Callable.From(() => OnGameStart(response)).CallDeferred();
                });
                
                WebSocketManager.Instance.Socket.On("playerAction", response =>
                {
                    Callable.From(() => OnPlayerAction(response)).CallDeferred();
                });
            }

            // 获取可选英雄列表
            await GetAvailableHeroes();

            // 获取节点引用
            _coinsLabel = GetNode<Label>("GameUI/ShopArea/TopBar/CoinsLabel");
            _tavernTierLabel = GetNode<Label>("GameUI/ShopArea/TopBar/TavernTierLabel");
            _menuButton = GetNode<Button>("GameUI/ShopArea/TopBar/MenuButton");
            _shopCardsContainer = GetNode<Control>("GameUI/ShopArea/ShopCards");
            _upgradeTavernButton = GetNode<Button>("GameUI/BottomBar/UpgradeButton");
            _refreshShopButton = GetNode<Button>("GameUI/BottomBar/RefreshButton");
            _freezeShopButton = GetNode<Button>("GameUI/BottomBar/FreezeButton");
            
            _playersListContainer = GetNode<Control>("GameUI/BattleArea/PlayersList");
            _battlefieldContainer = GetNode<Control>("GameUI/BattleArea/Battlefield");
            _upperBattlefield = GetNode<Control>("GameUI/BattleArea/Battlefield/UpperRow");
            _lowerBattlefield = GetNode<Control>("GameUI/BattleArea/Battlefield/LowerRow");
            
            _handCardsContainer = GetNode<Control>("GameUI/HandArea/Cards");
            _playerAvatar = GetNode<TextureRect>("GameUI/PlayerInfo/Avatar");
            _playerHealthLabel = GetNode<Label>("GameUI/PlayerInfo/HealthLabel");
            
            // 连接信号
            _menuButton.Pressed += OnMenuButtonPressed;
            _upgradeTavernButton.Pressed += OnUpgradeTavernPressed;
            _refreshShopButton.Pressed += OnRefreshShopPressed;
            _freezeShopButton.Pressed += OnFreezeShopPressed;

            _cardScene = GD.Load<PackedScene>("res://scenes/card.tscn");

            // 设置商店容器布局
            if (_shopCardsContainer is HBoxContainer hbox)
            {
                hbox.Alignment = BoxContainer.AlignmentMode.Center;  // 居中对齐
                hbox.CustomMinimumSize = new Vector2(0, 160);  // 减小高度
                hbox.SizeFlagsVertical = 0;  // 使用固定大小，不拉伸
                hbox.AddThemeConstantOverride("separation", 10);  // 设置卡牌间距
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Game._Ready() 发生错误: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        
        // 取消注册游戏场景的事件监听器
        if (WebSocketManager.Instance.Socket != null)
        {
            GD.Print("取消注册游戏场景的 Socket.IO 事件监听器");
            WebSocketManager.Instance.Socket.Off("gameStart");
            WebSocketManager.Instance.Socket.Off("playerAction");
        }
    }

    public override void _Process(double delta)
    {
        if (_isCountingDown)
        {
            _remainingTime -= delta;
            if (_remainingTime <= 0)
            {
                _isCountingDown = false;
                _heroSelectDialog.Hide();
                GD.Print("选择时间已到");
            }
            else
            {
                _timerLabel.Text = $"剩余时间: {_remainingTime:F0}秒";
            }
        }
    }

    private async Task GetAvailableHeroes()
    {
        try
        {
            var requestData = new Dictionary<string, string>
            {
                ["roomId"] = Global.Instance.CurrentRoomId
            };

            GD.Print($"请求获取英雄列表，房间ID: {Global.Instance.CurrentRoomId}");
            var response = await WebSocketManager.Instance.EmitAsync("getAvailableHeroes", requestData);
            GD.Print("获取英雄列表响应:", response.ToString());
            
            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                var data = response["data"].AsGodotDictionary();
                var heroes = data["heroes"].AsGodotArray();
                
                // 设置倒计时
                if (data.ContainsKey("selectionTimeLimit"))
                {
                    _remainingTime = data["selectionTimeLimit"].AsInt32();
                    _isCountingDown = true;
                    GD.Print($"开始倒计时: {_remainingTime}秒");
                }

                // 更新英雄按钮
                for (int i = 0; i < heroes.Count && i < _heroButtons.Length; i++)
                {
                    var hero = heroes[i].AsGodotDictionary();
                    var heroId = hero["id"].AsString();  // 使用 "id" 而不是 "_id"
                    var name = hero["name"].AsString();
                    
                    // 存储英雄ID映射
                    _heroIdMap[name] = heroId;
                    GD.Print($"添加英雄映射: {name} -> {heroId}");
                    
                    var description = hero["description"].AsString();
                    var ability = hero["ability"].AsGodotDictionary();
                    
                    var tooltipText = $"{description}\n\n技能: {ability["name"]}\n{ability["description"]}\n类型: {ability["type"]}\n费用: {ability["cost"]}";
                    
                    _heroButtons[i].Text = name;
                    _heroButtons[i].TooltipText = tooltipText;
                }

                // 显示英雄选择对话框
                _heroSelectDialog.Show();
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : "获取英雄列表失败";
                throw new Exception(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"获取英雄列表失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
            OS.Alert("获取英雄列表失败，请重试", "错误");
        }
    }

    private async void OnHeroSelected(string heroName)
    {
        GD.Print($"选择了英雄: {heroName}");
        
        try
        {
            if (!_heroIdMap.TryGetValue(heroName, out string heroId))
            {
                throw new Exception("找不到英雄ID");
            }

            var requestData = new Dictionary<string, string>
            {
                ["roomId"] = Global.Instance.CurrentRoomId,
                ["heroId"] = heroId
            };

            GD.Print($"发送确认英雄选择请求: roomId={requestData["roomId"]}, heroId={heroId}");
            var response = await WebSocketManager.Instance.EmitAsync("confirmHeroSelection", requestData);
            
            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                var data = response["data"].AsGodotDictionary();
                var userId = data["userId"].AsString();
                var selectedHeroId = data["heroId"].AsString();
                var allSelected = data["allSelected"].AsBool();
                var message = data["message"].AsString();
                
                GD.Print($"确认英雄选择成功: {message}");
                GD.Print($"用户ID: {userId}");
                GD.Print($"英雄ID: {selectedHeroId}");
                GD.Print($"所有玩家是否已选择: {allSelected}");
                
                // 禁用所有英雄按钮
                foreach (var button in _heroButtons)
                {
                    button.Disabled = true;
                }

                // 更新提示文本
                _timerLabel.Text = "等待其他玩家选择...";
                
                // 不立即关闭弹窗，等待倒计时结束
                if (allSelected)
                {
                    GD.Print("所有玩家已选择英雄，等待游戏开始");
                    // TODO: 显示等待游戏开始的提示
                }
                else
                {
                    GD.Print("等待其他玩家选择英雄");
                    // TODO: 显示等待其他玩家的提示
                }
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : "确认英雄选择失败";
                throw new Exception(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"确认英雄选择失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
            OS.Alert("确认英雄选择失败，请重试", "错误");
        }
    }

    private void OnGameStart(SocketIOClient.SocketIOResponse response)
    {
        try
        {
            var data = response.GetValue<JsonElement>(0);
            GD.Print("===== 游戏开始事件数据 =====");
            GD.Print(data.GetRawText());
            GD.Print("===========================");

            // 解析基本信息
            _gameId = data.GetProperty("gameId").GetString();
            _roomId = data.GetProperty("roomId").GetString();
            _currentTurn = data.GetProperty("turn").GetInt32();
            _currentPhase = data.GetProperty("phase").GetString();
            
            GD.Print($"游戏基本信息:");
            GD.Print($"- 游戏ID: {_gameId}");
            GD.Print($"- 房间ID: {_roomId}");
            GD.Print($"- 当前回合: {_currentTurn}");
            GD.Print($"- 游戏阶段: {_currentPhase}");

            // 解析玩家信息
            _players = new List<PlayerInfo>();
            var playersArray = data.GetProperty("players").EnumerateArray();
            GD.Print("\n玩家信息:");
            foreach (var playerElement in playersArray)
            {
                try
                {
                    var player = new PlayerInfo
                    {
                        UserId = playerElement.GetProperty("userId").GetString(),
                        Username = playerElement.GetProperty("username").GetString(),
                        HeroId = playerElement.GetProperty("heroId").GetString(),
                        IsBot = playerElement.GetProperty("isBot").GetBoolean(),
                        Health = playerElement.GetProperty("health").GetInt32(),
                        Coins = playerElement.GetProperty("coins").GetInt32(),
                        TavernTier = playerElement.GetProperty("tavernTier").GetInt32()
                    };

                    // 解析英雄信息
                    var heroData = playerElement.GetProperty("hero");
                    player.Hero = new HeroInfo
                    {
                        Id = heroData.GetProperty("id").GetString(),
                        Name = heroData.GetProperty("name").GetString(),
                        Description = heroData.GetProperty("description").GetString(),
                        Health = heroData.GetProperty("health").GetInt32(),
                        Ability = new AbilityInfo
                        {
                            Name = heroData.GetProperty("ability").GetProperty("name").GetString(),
                            Description = heroData.GetProperty("ability").GetProperty("description").GetString(),
                            Type = heroData.GetProperty("ability").GetProperty("type").GetString(),
                            Cost = heroData.GetProperty("ability").GetProperty("cost").GetInt32(),
                            Effect = heroData.GetProperty("ability").GetProperty("effect").GetString()
                        }
                    };

                    _players.Add(player);
                    GD.Print($"\n玩家 {player.Username}:");
                    GD.Print($"- 用户ID: {player.UserId}");
                    GD.Print($"- 是否机器人: {player.IsBot}");
                    GD.Print($"- 生命值: {player.Health}");
                    GD.Print($"- 金币: {player.Coins}");
                    GD.Print($"- 酒馆等级: {player.TavernTier}");
                    GD.Print($"- 英雄: {player.Hero.Name}");
                    GD.Print($"- 英雄技能: {player.Hero.Ability.Name} (费用: {player.Hero.Ability.Cost})");
                }
                catch (Exception e)
                {
                    GD.PrintErr($"解析玩家数据失败: {e.Message}");
                    continue;  // 跳过这个玩家，继续处理下一个
                }
            }

            // 保存当前用户的游戏信息
            var currentPlayer = _players.Find(p => p.UserId == Global.Instance.UserInfo.userId);
            if (currentPlayer != null)
            {
                Global.Instance.CurrentGameInfo = new UserGameInfo
                {
                    UserId = currentPlayer.UserId,
                    Username = currentPlayer.Username,
                    HeroId = currentPlayer.HeroId,
                    IsBot = currentPlayer.IsBot,
                    Health = currentPlayer.Health,
                    Coins = currentPlayer.Coins,
                    TavernTier = currentPlayer.TavernTier,
                    Hero = currentPlayer.Hero,
                    Board = currentPlayer.Board,
                    Hand = currentPlayer.Hand
                };
                GD.Print($"已保存当前用户的游戏信息: {currentPlayer.Username}");
            }
            else
            {
                GD.PrintErr($"找不到当前用户的游戏信息，用户ID: {Global.Instance.UserInfo.userId}");
            }

            // TODO: 初始化游戏界面
            InitializeGameUI();

            GD.Print("\n===== 游戏开始初始化完成 =====");
        }
        catch (Exception e)
        {
            GD.PrintErr("处理游戏开始事件失败:", e.Message);
            GD.PrintErr("错误堆栈:", e.StackTrace);
        }
    }

    private void InitializeGameUI()
    {
        // 更新商店区域
        UpdateShopUI();
        
        // 更新玩家列表
        UpdatePlayersList();
        
        // 更新战场
        UpdateBattlefield();
        
        // 更新手牌区域
        UpdateHandCards();
        
        // 更新玩家信息
        UpdatePlayerInfo();
    }
    private void UpdateBattlefield()
    {
        
    }
    private void UpdateHandCards()
    {
        
    }
    private void UpdatePlayerInfo()
    {
        
    }
    private void UpdateShopUI()
    {
        // 使用 CurrentGameInfo 更新商店UI
        if (Global.Instance.CurrentGameInfo != null)
        {
            _coinsLabel.Text = $"金币: {Global.Instance.CurrentGameInfo.Coins}";
            _tavernTierLabel.Text = $"酒馆等级: {Global.Instance.CurrentGameInfo.TavernTier}";
        }
    }

    private void UpdatePlayersList()
    {
        // 清空现有列表
        foreach (Node child in _playersListContainer.GetChildren())
        {
            child.QueueFree();
        }

        // 添加所有玩家
        foreach (var player in _players)
        {
            var playerButton = new Button
            {
                Text = player.Username,
                TooltipText = $"英雄: {player.Hero.Name}\n" +
                             $"生命值: {player.Health}\n" +
                             $"技能: {player.Hero.Ability.Name}\n" +
                             $"{player.Hero.Ability.Description}"
            };
            _playersListContainer.AddChild(playerButton);
        }
    }

    private void OnPlayerAction(SocketIOClient.SocketIOResponse response)
    {
        GD.Print("收到玩家动作");
        // TODO: 处理玩家动作逻辑
    }

    private void OnMenuButtonPressed()
    {
        // TODO: 实现菜单按钮的逻辑
    }

    private void OnUpgradeTavernPressed()
    {
        // TODO: 实现升级酒馆按钮的逻辑
    }

    private async void OnRefreshShopPressed()
    {
        try
        {
            _refreshShopButton.Disabled = true;

            GD.Print("\n===== 开始刷新商店 =====");

            var requestData = new Dictionary<string, string>
            {
                ["roomId"] = Global.Instance.CurrentRoomId
            };
            GD.Print($"发送刷新商店请求: roomId={Global.Instance.CurrentRoomId}");

            var response = await WebSocketManager.Instance.EmitAsync("refreshShop", requestData);
            GD.Print("\n===== 刷新商店响应数据 =====");
            GD.Print(response.ToString());

            if (response.TryGetValue("success", out var successValue) && (bool)successValue)
            {
                var data = response["data"].AsGodotDictionary();
                var minionsArray = data["minions"].AsGodotArray();
                var remainingCoins = data["remainingCoins"].AsInt32();

                // 更新金币显示
                _coinsLabel.Text = $"金币: {remainingCoins}";

                // 清空现有商店卡牌
                foreach (var card in _shopCards)
                {
                    if (!_handCards.Contains(card))  // 只删除不在手牌中的卡牌
                    {
                        card.QueueFree();
                    }
                }
                _shopCards.Clear();

                // 添加新的随从卡牌
                foreach (var minionData in minionsArray)
                {
                    var minionDict = minionData.AsGodotDictionary();
                    
                    // 创建卡牌实例
                    var card = _cardScene.Instantiate<Card>();
                    
                    // 初始化卡牌数据
                    card.Initialize(
                        name: minionDict["name"].AsString(),
                        attack: minionDict["attack"].AsInt32(),
                        health: minionDict["health"].AsInt32(),
                        description: minionDict["description"].AsString()
                    );

                    // 设置卡牌布局
                    card.CustomMinimumSize = new Vector2(CARD_WIDTH, CARD_HEIGHT);
                    card.SizeFlagsHorizontal = 0;
                    card.SizeFlagsVertical = 0;
                    
                    // 添加到商店容器
                    _shopCardsContainer.AddChild(card);
                    _shopCards.Add(card);
                }

                GD.Print($"\n刷新商店完成，剩余金币: {remainingCoins}");
            }
            else
            {
                string errorMessage = response.ContainsKey("error") ? 
                    (string)response["error"] : "刷新商店失败";
                throw new Exception(errorMessage);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr("\n===== 刷新商店失败 =====");
            GD.PrintErr($"错误信息: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
            OS.Alert("刷新商店失败，请重试", "错误");
        }
        finally
        {
            _refreshShopButton.Disabled = false;
        }
    }

    private void OnFreezeShopPressed()
    {
        // TODO: 实现冻结商店按钮的逻辑
    }

    public void AddToHand(Card card)
    {
        // 从商店移除
        if (_shopCards.Contains(card))
        {
            _shopCards.Remove(card);
        }
        
        // 保存卡牌的全局位置
        Vector2 originalGlobalPos = card.GlobalPosition;
        
        // 添加到手牌
        _handCards.Add(card);
        
        // 重新设置卡牌的布局属性
        card.SizeFlagsHorizontal = 0;
        card.SizeFlagsVertical = 0;
        card.CustomMinimumSize = new Vector2(CARD_WIDTH, CARD_HEIGHT);
        
        // 重新设置父节点
        card.Reparent(_handCardsContainer);
        
        // 设置卡牌位置，保持在原来的全局位置
        card.GlobalPosition = originalGlobalPos;
        
        // 重新排列所有手牌
        ArrangeHandCards();
    }

    private void ArrangeHandCards()
    {
        int cardCount = _handCards.Count;
        if (cardCount == 0) return;

        float containerWidth = _handCardsContainer.Size.X;
        float cardWidth = CARD_WIDTH;
        float cardHeight = CARD_HEIGHT;
        
        // 计算卡牌间距，确保不会超出容器宽度
        float spacing = Mathf.Min(
            cardWidth * 0.7f,  // 最大重叠量（卡牌宽度的70%）
            (containerWidth - cardWidth) / (cardCount - 1)  // 平均分布所需的间距
        );

        // 计算总宽度和起始X位置，确保居中
        float totalWidth = spacing * (cardCount - 1) + cardWidth;
        float startX = (containerWidth - totalWidth) / 2;
        float startY = (_handCardsContainer.Size.Y - cardHeight) / 2;

        // 创建一个主 Tween 对象来管理所有卡牌的动画
        var tween = CreateTween();
        tween.SetParallel(true); // 所有卡牌同时移动
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.Out);

        // 设置每张卡牌的位置
        for (int i = 0; i < cardCount; i++)
        {
            var card = _handCards[i];
            Vector2 targetPos = new Vector2(startX + i * spacing, startY);
            
            // 添加位置动画
            tween.TweenProperty(card, "position", targetPos, 0.3f);
            
            // 设置层级
            card.ZIndex = i;
        }
    }

    // 根据鼠标X坐标获取手牌索引
    public int GetHandCardIndexAtPosition(float mouseX)
    {
        if (_handCards.Count == 0) return 0;

        float containerWidth = _handCardsContainer.Size.X;
        float cardWidth = CARD_WIDTH;
        
        // 计算卡牌间距
        float spacing = Mathf.Min(
            cardWidth * 0.7f,
            (containerWidth - cardWidth) / (_handCards.Count - 1)
        );

        // 计算起始X位置
        float totalWidth = spacing * (_handCards.Count - 1) + cardWidth;
        float startX = _handCardsContainer.GlobalPosition.X + (containerWidth - totalWidth) / 2;

        // 计算鼠标位置对应的索引
        float relativeX = mouseX - startX;
        int index = Mathf.RoundToInt(relativeX / spacing);
        
        // 确保索引在有效范围内
        return Mathf.Clamp(index, 0, _handCards.Count - 1);
    }

    // 重新排序手牌
    public void ReorderHandCard(Card card, int newIndex)
    {
        int oldIndex = _handCards.IndexOf(card);
        if (oldIndex == -1 || oldIndex == newIndex) return;

        // 移除并插入到新位置
        _handCards.RemoveAt(oldIndex);
        _handCards.Insert(newIndex, card);

        // 重新排列所有手牌
        ArrangeHandCards();
    }
}

// 数据类型定义
public class PlayerInfo
{
    public string UserId { get; set; }
    public string Username { get; set; }
    public string HeroId { get; set; }
    public bool IsBot { get; set; }
    public int Health { get; set; }
    public int Coins { get; set; }
    public int TavernTier { get; set; }
    public HeroInfo Hero { get; set; }
    public List<object> Board { get; set; } = new List<object>();  // TODO: 定义随从类型
    public List<object> Hand { get; set; } = new List<object>();   // TODO: 定义卡牌类型
}

public class HeroInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int Health { get; set; }
    public AbilityInfo Ability { get; set; }
}

public class AbilityInfo
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Type { get; set; }
    public int Cost { get; set; }
    public string Effect { get; set; }
} 