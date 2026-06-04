# Rolan

仿 Rolan 的 Windows 桌面快捷方式管理器，基于 .NET 8 + WPF。

## 功能

- 系统托盘常驻
- 全局热键呼出/隐藏面板
- 分组管理快捷方式
- 搜索、拖拽添加、浏览添加
- 快捷方式编辑、删除、移动、排序
- 导入 / 导出数据
- 主题切换、置顶、鼠标穿透、贴边自动隐藏
- 开机自启

## 环境要求

- Windows
- .NET 8 SDK

## 运行

```bash
dotnet restore src/Rolan/Rolan.sln
dotnet run --project src/Rolan/Rolan/Rolan.csproj
```

## 数据位置

- 数据库：`%AppData%\Rolan\data.db`
- 设置：`%AppData%\Rolan\settings.json`

## 默认热键

- `Ctrl + Alt + R`

## 项目结构

- `src/Rolan/Rolan/Views`：窗口和界面
- `src/Rolan/Rolan/ViewModels`：状态与命令
- `src/Rolan/Rolan/Services`：数据、热键、外壳、主题、启动服务
- `src/Rolan/Rolan/Models`：数据模型

