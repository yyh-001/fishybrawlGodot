# Fishy Brawl

一个在线多人对战卡牌游戏，使用 Godot 4.x 和 C# 开发。

## 技术栈

- **游戏引擎**: Godot 4.x
- **开发语言**: C#
- **后端**: Node.js + Socket.IO
- **数据库**: MongoDB

## 项目结构

```
FishyBrawl/
├── docs/               # 文档目录
│   ├── api.md         # API 文档
│   └── card.md        # 卡牌系统文档
├── scenes/            # 场景文件
│   ├── login.tscn     # 登录场景
│   └── main_menu.tscn # 主菜单场景
├── scripts/           # 脚本文件
│   ├── Login.cs       # 登录逻辑
│   ├── MainMenu.cs    # 主菜单逻辑
│   └── WebSocketManager.cs # WebSocket 管理器
└── README.md
```

## 功能特性

- 用户系统
  - 登录/注册
  - JWT 认证
- 好友系统
  - 好友列表
  - 在线状态
  - 添加好友
- 对战系统
  - 快速匹配
  - 创建房间
  - 加入房间

## 开发环境设置

1. 安装 Godot 4.x .NET 版本
2. 安装 .NET SDK 6.0 或更高版本
3. 克隆项目并打开解决方案

```bash
git clone https://github.com/yyh-001/FishyBrawlGodot.git
cd FishyBrawl
```

## 依赖项

- SocketIOClient (NuGet 包)
- System.Text.Json

## 运行项目

1. 启动后端服务器
```bash
cd backend
npm install
npm start
```

2. 在 Godot 编辑器中运行项目
   - 打开项目
   - 按 F5 或点击"运行项目"按钮

## 贡献指南

1. Fork 项目
2. 创建特性分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 许可证

[MIT License](LICENSE) 