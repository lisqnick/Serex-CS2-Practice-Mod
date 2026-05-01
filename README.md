# Serex-CS2-Practice-Mod
<img width="1280" height="720" alt="practicemodv1" src="https://github.com/user-attachments/assets/15e84bfd-4fe0-4ead-b505-8324fe5b793b" />
Serex's CS2 Practice Mod 是一个为自托管 CS2 服务器设计的自定义游戏模式，需要 Metamod 和 CounterStrikeSharp！该插件提供 30 多个实用练习命令，包括 grenade lineups 记忆、bot 管理、run boost 练习等功能。支持 Linux 和 Windows 服务器，可用于 offline 与 online 环境！

## ⚡️ Features / 功能

### 🌪 Practice:
- **Grenade Practice**: 无限 grenades、轨迹可视化、投掷统计
- **Rethrow System**: 返回上一次投掷位置，或重新投掷 grenade
- **Save/Load Positions**: 保存不限数量的 grenade lineups
- **Noclip & God Mode**: 自由飞行并以无敌状态练习
- **Spawn Browsing**: 循环浏览 T/CT spawns
- **Flash Immunity**: 将 flash 效果降到最低（正在寻找懂得如何修改颜色的人）
- **Damage Stats**: 追踪 grenade 伤害，并可按需重置

### 🐀 Bots:
- **Add Bots**: 智能队伍平衡
- **Place Bots**: 将 bots 放置到你当前位置
- **Bot Control**: 让 bots crouch/stand/jump
- **Remove Bots**: 瞄准 bot 单独移除，或一次性移除全部 bots
- **Auto-Respawn**: bots 死亡后会在死亡/放置位置 3 秒后重生
- **Run Boost**: 队友 bots 会跟随你，方便练习 run boosts！

### 🛠️ Utility:
- **Clear**: 移除所有 grenades 和效果
- **Impacts**: 开关 bullet impact markers
- **Restart**: 重启当前回合
- **Map Change**: 更换地图
- **GO/TP**: 传送到指定坐标

## 🗿 Requirements / 要求:

- **CS2 Dedicated Server** (Windows or Linux)
- **Metamod** (v2.0+)
- **CounterStrikeSharp** (v1.0.347)

## 📥 Quick Start / 快速开始:

详细安装步骤请查看 [INSTALLATION.md](INSTALLATION.md)。

## 💬 Command List / 命令列表:

### Practice Commands
| Command | Description |
|---------|---------|
| `!practice` & `!prac` | 开关 practice mode |
| `!help` | 显示 command list |
| `!back` | 返回上一次投掷位置 |
| `!rethrow` | 重新投掷上一颗 grenade |
| `!rec` | 记录你的 nade lineup |
| `!loadnade <#>` | 加载已保存位置 |
| `!listnades` | 列出所有已保存位置 |
| `!deletenade <#>` | 删除已保存位置 |
| `!replay <#>` | 加载并 replay 位置，同时给予 grenade |
| `!noclip` | 开关 fly mode |
| `!god` | 开关 invincibility |
| `!clear` | 清除 grenades 和 smokes |
| `!impacts` | 开关 bullet impacts |
| `!go <x, y, z>` | 传送到指定坐标 |
| `!tspawn [#]` | 浏览 T spawn points |
| `!ctspawn [#]` | 浏览 CT spawn points |
| `!map <name>` | 更换地图 |
| `!noflash` | 开关 flash immunity |
| `!restart` | 重启当前回合 |
| `!damage` & `!stats` | 显示 grenade damage stats |
| `!clearstats` | 清除 damage stats |

### Bot Commands
| Command | Description |
|---------|---------|
| `!bot` | 添加一个 bot（自动平衡队伍） |
| `!ctbot` | 添加一个 CT bot |
| `!tbot` | 添加一个 T bot |
| `!nobot` | 移除你正在瞄准的 bot |
| `!kickbots` | 移除所有 bots |
| `!place` | 将 bot 放置到你当前位置 |
| `!cplace` | 将 T bot 放置到你当前位置 |
| `!ctplace` | 将 CT bot 放置到你当前位置 |
| `!boost` | 让所有 bots crouch |
| `!stand` | 让所有 bots stand |
| `!jump` | 让最近的 bot jump |

## 🔮 Tips / 提示:

### Bind Commands
可以用 `bind K css_name` 绑定任意命令。例如 `css_boost`
会在按下 "K" 时让 bots crouch。你可以把所有常用命令绑定到按键上，以便更快使用！

### Saving Grenade Lineups
1. 站到准备投掷的位置
2. 使用 `!savenade <name>`（例如 `!savenade mirage_a_smoke`）
3. 你的位置、角度和当前 grenade 会被保存
4. 使用 `!listnades` 查看所有已保存位置
5. 使用 `!loadnade 1` 返回该 lineup

### Run Boost Practice
1. 添加一个队友 bot：`!ctbot`（或 `!tbot`）
2. 放置 bot：`!place`
3. 让 bot crouch：`!boost`
4. 跳到 bot 头上并向前跑
5. 跳下以获得接近真实的 run boost 动量！

### Grenade Practice
- 所有 grenades 会自动补充
- 投掷统计会显示 airtime 和 distance
- 使用 `!back` 快速重试投掷
- `!damage` 显示造成的总伤害

## 👁 Credits:
**Author**: Serex

**Version**: 1.2.2

*兼容 latest CS2 updates*

## ⛔️ License / 许可证:

可在个人和社区服务器中免费使用和修改。

---

**祝你练习愉快！♻️🔫**
