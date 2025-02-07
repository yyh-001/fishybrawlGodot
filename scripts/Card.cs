using Godot;
using System;

public partial class Card : Control
{
    private Label _nameLabel;
    private TextureRect _image;
    private Label _attackLabel;
    private Label _healthLabel;
    private Panel _background;
    private bool _isDragging = false;
    private Vector2 _dragStart;
    private Vector2 _originalPosition;
    private Control _handArea;  // 手牌区域引用
    private int _originalZIndex;  // 添加字段保存原始 ZIndex

    [Signal]
    public delegate void CardClickedEventHandler(Card card);

    public string CardName { get; private set; }
    public int Attack { get; private set; }
    public int Health { get; private set; }
    public string Description { get; private set; }
    public string ImagePath { get; private set; }

    public override void _Ready()
    {
        try
        {
            // 获取节点引用
            _nameLabel = GetNode<Label>("MarginContainer/VBoxContainer/NameLabel");
            _image = GetNode<TextureRect>("MarginContainer/VBoxContainer/Image");
            _attackLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatsContainer/AttackLabel");
            _healthLabel = GetNode<Label>("MarginContainer/VBoxContainer/StatsContainer/HealthLabel");
            _background = GetNode<Panel>("Background");

            // 设置拖放功能
            MouseFilter = MouseFilterEnum.Stop;
            GuiInput += OnGuiInput;

            // 打印节点路径，用于调试
            GD.Print("\n卡牌节点初始化:");
            GD.Print($"- 名称标签: {_nameLabel != null}");
            GD.Print($"- 图片: {_image != null}");
            GD.Print($"- 攻击力标签: {_attackLabel != null}");
            GD.Print($"- 生命值标签: {_healthLabel != null}");
            GD.Print($"- 背景面板: {_background != null}");

            // 获取手牌区域引用
            _handArea = GetNode<Control>("/root/Game/GameUI/HandArea/Cards");
        }
        catch (Exception e)
        {
            GD.PrintErr($"卡牌初始化失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    public void Initialize(string name, int attack, int health, string description, string imagePath = null)
    {
        try
        {
            GD.Print($"\n开始初始化卡牌 {name}:");
            
            // 等待一帧确保节点已准备好
            if (!IsNodeReady())
            {
                CallDeferred(nameof(Initialize), name, attack, health, description, imagePath);
                return;
            }

            CardName = name;
            Attack = attack;
            Health = health;
            Description = description;
            ImagePath = imagePath;

            // 确保所有节点引用都存在
            if (_nameLabel == null || _attackLabel == null || _healthLabel == null)
            {
                GD.PrintErr("卡牌节点未正确初始化");
                return;
            }

            GD.Print("节点检查通过，开始更新显示");
            UpdateDisplay();
            GD.Print("卡牌初始化完成");
        }
        catch (Exception e)
        {
            GD.PrintErr($"卡牌初始化失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    private bool IsNodeReady()
    {
        return IsInsideTree() && _nameLabel != null;
    }

    private void UpdateDisplay()
    {
        try
        {
            GD.Print($"\n更新卡牌显示:");
            GD.Print($"- 名称: {CardName}");
            GD.Print($"- 攻击力: {Attack}");
            GD.Print($"- 生命值: {Health}");
            GD.Print($"- 描述: {Description}");

            _nameLabel.Text = CardName;
            _attackLabel.Text = Attack.ToString();
            _healthLabel.Text = Health.ToString();
            TooltipText = Description;  // 将描述设置为悬浮提示

            if (!string.IsNullOrEmpty(ImagePath) && _image != null)
            {
                _image.Texture = GD.Load<Texture2D>(ImagePath);
            }
            
            GD.Print("显示更新完成");
        }
        catch (Exception e)
        {
            GD.PrintErr($"更新卡牌显示失败: {e.Message}");
            GD.PrintErr($"错误堆栈: {e.StackTrace}");
        }
    }

    private void OnGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left)
            {
                if (mouseButton.Pressed)
                {
                    // 开始拖动
                    _isDragging = true;
                    _dragStart = GetGlobalMousePosition();
                    _originalPosition = GlobalPosition;
                    // 保存当前的 ZIndex
                    _originalZIndex = ZIndex;
                    // 设置为最高层
                    ZIndex = 1000;
                }
                else if (_isDragging)
                {
                    // 结束拖动
                    _isDragging = false;
                    
                    var mousePos = GetGlobalMousePosition();
                    var handArea = GetNode<Control>("/root/Game/GameUI/HandArea");
                    var handRect = handArea.GetGlobalRect();
                    var game = GetNode<Game>("/root/Game");
                    
                    if (handRect.HasPoint(mousePos))
                    {
                        if (GetParent().Name == "Cards") // 如果是手牌重新排序
                        {
                            // 计算新的位置索引
                            int newIndex = game.GetHandCardIndexAtPosition(mousePos.X);
                            game.ReorderHandCard(this, newIndex);
                        }
                        else
                        {
                            // 从商店添加到手牌
                            game.AddToHand(this);
                        }
                    }
                    else
                    {
                        // 返回原位并恢复原来的 ZIndex
                        GlobalPosition = _originalPosition;
                        ZIndex = _originalZIndex;
                    }
                }
            }
        }
        else if (@event is InputEventMouseMotion && _isDragging)
        {
            // 更新卡牌位置
            Vector2 offset = GetGlobalMousePosition() - _dragStart;
            GlobalPosition = _originalPosition + offset;
        }
    }

    public void SetHighlight(bool highlight)
    {
        if (highlight)
        {
            // TODO: 添加高亮效果
            Modulate = new Color(1.2f, 1.2f, 1.2f);
        }
        else
        {
            Modulate = Colors.White;
        }
    }

    private Control GetHandCardsContainer()
    {
        // 使用正确的路径
        return GetNode<Control>("/root/Game/GameUI/HandArea/Cards");
    }
} 