# Rolan

仿 Rolan 的 Windows 桌面快捷方式管理器，基于 .NET 8 + WPF。

## 功能

- 系统托盘常驻
- 单实例运行，重复启动会唤醒已运行面板，避免重复托盘图标和热键冲突
- 托盘菜单操作会自动唤醒面板并聚焦搜索框
- 全局热键呼出/隐藏面板
- 分组管理快捷方式
- 标题栏添加菜单集中提供文件、路径/网址、文件夹、系统命令、开始菜单导入和桌面导入入口
- 搜索、拖拽添加、浏览添加文件/文件夹
- 手动添加文件路径、文件夹路径、URL
- 可添加内置系统命令：锁定电脑、文件资源管理器、Windows 设置、控制面板、任务管理器
- 可从 Windows 开始菜单批量导入应用快捷方式
- 可从桌面批量导入快捷方式
- 添加和批量导入会跳过当前分组内的重复目标
- 支持环境变量路径和相对路径
- 无法提取图标时显示类型兜底标识，避免空白图标
- 搜索框回车启动当前选中项，`:g 关键词` / `:b 关键词` 发起网页搜索
- 搜索会匹配名称、目标路径、启动参数、工作目录、快捷方式类型和中文首字母，搜索结果会显示来源分组
- 面板获得焦点后可直接键入开始搜索
- 搜索框支持上下键切换选中项，Enter 启动当前选中项
- `Alt + 1..9` 可直接启动当前列表第 1 到第 9 个快捷方式
- 搜索框和快捷方式网格支持 F2 编辑、Ctrl+D 复制、Ctrl+O 打开文件位置；快捷方式网格支持 Delete 删除，搜索框为空时 Delete 删除当前选中项
- 快捷方式编辑、复制、删除、移动、排序，支持拖拽重排和跨分组移动
- 删除快捷方式会二次确认，避免误删
- 右键菜单会按快捷方式类型禁用无意义操作
- 支持编辑启动参数和工作目录
- 分组支持右键管理、拖拽排序，新增和重命名会校验空白名与重复名；删除分组时可先把快捷方式移动到其他分组
- 导入 / 导出数据
- 导入数据时会规范化排序、补齐空名称并跳过无效目标
- 导入完成后自动切换到第一个导入分组
- 主题切换、置顶、鼠标穿透、可选贴边自动隐藏
- 开机自启到托盘、可配置全局热键
- 开机自启状态会校验当前 exe 路径，便携目录移动后不会误报

## 环境要求

- Windows
- .NET 8 SDK

## 运行

```bash
dotnet restore src/Rolan/Rolan.sln
dotnet run --project src/Rolan/Rolan/Rolan.csproj
```

## 发布免安装版

```bash
dotnet publish src/Rolan/Rolan/Rolan.csproj -c Release -r win-x64 --self-contained true -o .\publish-rolan -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
```

## 数据位置

- 普通运行：`%AppData%\Rolan\data.db` 和 `%AppData%\Rolan\settings.json`
- 免安装发布目录包含 `portable.flag` 或本地 `data\` 目录时：数据写入 `Rolan.exe` 同目录下的 `data\`
- 也可以用环境变量 `ROLAN_DATA_DIR` 指定自定义数据目录

## 默认热键

- 默认 `Alt + Space`
- 可在设置窗口调整热键组合
- 如果默认 `Alt + Space` 注册失败，会回退到 `Ctrl + Alt + R`

## 项目结构

- `src/Rolan/Rolan/Views`：窗口和界面
- `src/Rolan/Rolan/ViewModels`：状态与命令
- `src/Rolan/Rolan/Services`：数据、热键、外壳、主题、启动服务
- `src/Rolan/Rolan/Models`：数据模型

