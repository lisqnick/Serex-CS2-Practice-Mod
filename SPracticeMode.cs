using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Entities;

namespace PracticeMode;

public class GrenadePosition
{
    public string Name { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Pitch { get; set; }
    public float Yaw { get; set; }
    public float Roll { get; set; }
    public string GrenadeType { get; set; }
    public List<MovementFrame> MovementFrames { get; set; }
    public string Technique { get; set; }
    public bool HasRecording { get; set; }
    
    // Throw position
    public float ThrowX { get; set; }
    public float ThrowY { get; set; }
    public float ThrowZ { get; set; }
    public float ThrowPitch { get; set; }
    public float ThrowYaw { get; set; }
    public float ThrowRoll { get; set; }
    
    public GrenadePosition()
    {
        Name = "";
        GrenadeType = "";
        MovementFrames = new List<MovementFrame>();
        Technique = "";
        HasRecording = false;
    }
}

public class MovementFrame
{
    public float Time { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float AngleX { get; set; }
    public float AngleY { get; set; }
    public float AngleZ { get; set; }
    public ulong Buttons { get; set; }
    public bool IsJumping { get; set; }
    public bool IsCrouching { get; set; }
    public bool IsWalking { get; set; }
    public float Velocity { get; set; }
}

public class GrenadeRecording
{
    public Vector StartPosition { get; set; }
    public QAngle StartAngle { get; set; }
    public List<MovementFrame> Frames { get; set; }
    public string GrenadeType { get; set; }
    public Vector ThrowPosition { get; set; }
    public QAngle ThrowAngle { get; set; }
    public float RecordStartTime { get; set; }
    public string DetectedTechnique { get; set; }
    
    public GrenadeRecording()
    {
        StartPosition = new Vector(0, 0, 0);
        StartAngle = new QAngle(0, 0, 0);
        Frames = new List<MovementFrame>();
        GrenadeType = "";
        ThrowPosition = new Vector(0, 0, 0);
        ThrowAngle = new QAngle(0, 0, 0);
        DetectedTechnique = "";
    }
}

public class ReplayState
{
    public GrenadePosition Nade { get; set; }
    public int CurrentFrame { get; set; }
    public float StartTime { get; set; }
    public Vector OriginalPosition { get; set; }
    public QAngle OriginalAngle { get; set; }
    
    public ReplayState()
    {
        Nade = null!;
        CurrentFrame = 0;
        StartTime = 0;
        OriginalPosition = new Vector(0, 0, 0);
        OriginalAngle = new QAngle(0, 0, 0);
    }
}

public class ThrowData
{
    public float StartTime { get; set; }
    public Vector StartPos { get; set; } = new Vector(0, 0, 0);
    public string GrenadeType { get; set; } = "";
}

public class BotStateInfo
{
    public bool IsCrouched { get; set; }
    public bool IsWalking { get; set; }
    public float Speed { get; set; }
}

public class PracticeMode : BasePlugin
{
    public override string ModuleName => "Serex Practice Mod";
    public override string ModuleVersion => "1.2.2";
    public override string ModuleDescription => "Advanced CS2 Practice Mod";

    private bool _practiceMode = false;
    private readonly Dictionary<ulong, List<GrenadePosition>> _savedNades = new();
    private readonly Dictionary<ulong, Vector> _lastPosition = new();
    private readonly Dictionary<ulong, QAngle> _lastAngle = new();
    private readonly Dictionary<ulong, string> _lastGrenadeType = new();
    private readonly Dictionary<ulong, bool> _godMode = new();
    private readonly Dictionary<ulong, bool> _showImpacts = new();
    private readonly Dictionary<ulong, ThrowData> _activeThrows = new();
    private readonly Dictionary<ulong, int> _selectedTSpawn = new();
    private readonly Dictionary<ulong, int> _selectedCTSpawn = new();
    private readonly Dictionary<ulong, bool> _noFlash = new();
    private readonly Dictionary<ulong, int> _lastBotIndex = new();
    private readonly Dictionary<string, bool> _botCrouchState = new();
    private readonly Dictionary<string, Vector> _botRespawnPosition = new();
    private readonly Dictionary<string, QAngle> _botRespawnAngle = new();
	private readonly Dictionary<ulong, int> _totalGrenadeDamage = new();
	private readonly Dictionary<ulong, Dictionary<string, int>> _grenadeDamageByType = new();
	private readonly Dictionary<string, ulong> _botFollowPlayer = new();
	private readonly Dictionary<ulong, string> _playerOnBotHead = new();
	private readonly Dictionary<ulong, BotStateInfo> _botStateWhileOn = new();
	private readonly Dictionary<ulong, GrenadeRecording> _activeRecordings = new();
	private readonly Dictionary<ulong, GrenadeRecording> _pendingSaves = new();
	private readonly Dictionary<ulong, ReplayState> _activeReplays = new();
	private CounterStrikeSharp.API.Modules.Timers.Timer? _replayTimer;
	private CounterStrikeSharp.API.Modules.Timers.Timer? _recordingTimer;
	private CounterStrikeSharp.API.Modules.Timers.Timer? _runBoostTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _tickTimer;
    private CounterStrikeSharp.API.Modules.Timers.Timer? _flashCheckTimer;
	private CounterStrikeSharp.API.Modules.Timers.Timer? _botCrouchTimer;
    private string _pluginPath = "";
    private bool _impactsEnabled = true;

    public override void Load(bool hotReload)
    {
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventGrenadeThrown>(OnGrenadeThrown);
        RegisterEventHandler<EventSmokegrenadeDetonate>(OnSmokeDetonate);
        RegisterEventHandler<EventHegrenadeDetonate>(OnHeDetonate);
        RegisterEventHandler<EventFlashbangDetonate>(OnFlashDetonate);
        RegisterEventHandler<EventMolotovDetonate>(OnMolotovDetonate);
        RegisterEventHandler<EventDecoyStarted>(OnDecoyStarted);
        RegisterEventHandler<EventPlayerBlind>(OnPlayerBlind);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        
        _pluginPath = ModuleDirectory;
        
        Console.WriteLine($"[{ModuleName}] v{ModuleVersion} loaded!");
        Console.WriteLine($"[{ModuleName}] Save path: {_pluginPath}");
    }

    public override void Unload(bool hotReload)
	{
		Console.WriteLine($"[{ModuleName}] Starting unload process...");
		
		try
		{
			// Kill all timers
			_tickTimer?.Kill();
			_tickTimer = null;
			
			_flashCheckTimer?.Kill();
			_flashCheckTimer = null;
			
			_botCrouchTimer?.Kill();
			_botCrouchTimer = null;
			
			_runBoostTimer?.Kill();
			_runBoostTimer = null;
			
			_recordingTimer?.Kill();
			_recordingTimer = null;
			
			_replayTimer?.Kill();
			_replayTimer = null;
			
			Console.WriteLine($"[{ModuleName}] Timers killed");
			
			// Clear all dictionaries
			_savedNades.Clear();
			_lastPosition.Clear();
			_lastAngle.Clear();
			_lastGrenadeType.Clear();
			_godMode.Clear();
			_showImpacts.Clear();
			_activeThrows.Clear();
			_selectedTSpawn.Clear();
			_selectedCTSpawn.Clear();
			_noFlash.Clear();
			_lastBotIndex.Clear();
			_botCrouchState.Clear();
			_botRespawnPosition.Clear();
			_botRespawnAngle.Clear();
			_totalGrenadeDamage.Clear();
			_grenadeDamageByType.Clear();
			_botFollowPlayer.Clear();
			_playerOnBotHead.Clear();
			_botStateWhileOn.Clear();
			_activeRecordings.Clear();
			_pendingSaves.Clear();
			_activeReplays.Clear();
			
			Console.WriteLine($"[{ModuleName}] Dictionaries cleared");
			
			// Reset practice mode
			_practiceMode = false;
			
			Console.WriteLine($"[{ModuleName}] v{ModuleVersion} unloaded successfully!");
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[{ModuleName}] Error during unload: {ex.Message}");
			Console.WriteLine($"[{ModuleName}] Stack trace: {ex.StackTrace}");
		}
	}

    [ConsoleCommand("css_practice", "Toggle practice mode")]
	[ConsoleCommand("css_prac", "Toggle practice mode")]
	public void OnPracticeCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null) return;

		_practiceMode = !_practiceMode;

		if (_practiceMode)
		{
			EnablePracticeMode();
			player.PrintToChat($" {ChatColors.Yellow}[Practice]{ChatColors.Default} Practice mode {ChatColors.Green}ENABLED!");
			player.PrintToChat($" {ChatColors.Yellow}[Practice]{ChatColors.Default} Type {ChatColors.Green}!help{ChatColors.Default} for commands");
			player.PrintToChat($" {ChatColors.Yellow}[Practice]{ChatColors.Default} Show support & say thanks by checking out {ChatColors.Green}Serex{ChatColors.Default} on {ChatColors.Red}YouTube");
			
			if (_tickTimer == null)
			{
				_tickTimer = AddTimer(0.5f, () => GiveGrenadesToAll(), TimerFlags.REPEAT);
			}
			
			// Start flash immunity checker
			if (_flashCheckTimer == null)
			{
				_flashCheckTimer = AddTimer(0.1f, () => CheckFlashImmunity(), TimerFlags.REPEAT);
			}			
			// Run boost tracking
			if (_runBoostTimer == null)
			{
				_runBoostTimer = AddTimer(0.05f, () => UpdateRunBoost(), TimerFlags.REPEAT);
			}
			// Grenade lineup recording
			if (_recordingTimer == null)
			{
				_recordingTimer = AddTimer(0.01f, () => RecordMovement(), TimerFlags.REPEAT);
			}
			// Replay timer
			if (_replayTimer == null)
			{
				_replayTimer = AddTimer(0.01f, () => UpdateReplays(), TimerFlags.REPEAT);
			}
		}
		else
		{
			DisablePracticeMode();
			player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Practice mode DISABLED!");
			_tickTimer?.Kill();
			_tickTimer = null;
			_flashCheckTimer?.Kill();
			_flashCheckTimer = null;
			_botCrouchTimer?.Kill();
			_botCrouchTimer = null;
			_runBoostTimer?.Kill();
			_runBoostTimer = null;
			_recordingTimer?.Kill();
			_recordingTimer = null;
			_replayTimer?.Kill();
			_replayTimer = null;
		}
	}

    [ConsoleCommand("css_help", "Show available commands")]
    [ConsoleCommand("css_phelp", "Show available commands")]
    public void OnHelpCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null) return;

        player.PrintToChat($" {ChatColors.Green}═══════ PRACTICE MODE COMMANDS ═══════");
        player.PrintToChat($" {ChatColors.Yellow}!practice{ChatColors.Default} - Toggle practice mode");
        player.PrintToChat($" {ChatColors.Yellow}!back{ChatColors.Default} - Return to last nade position");
		player.PrintToChat($" {ChatColors.Yellow}!rethrow{ChatColors.Default} - Rethrow last thrown grenade");
        player.PrintToChat($" {ChatColors.Yellow}!rec{ChatColors.Default} / {ChatColors.Yellow}!record{ChatColors.Default} - Start recording nade lineup");
        player.PrintToChat($" {ChatColors.Yellow}!loadnade <#>{ChatColors.Default} - Load saved position");
        player.PrintToChat($" {ChatColors.Yellow}!listnades{ChatColors.Default} - List all saved positions");
        player.PrintToChat($" {ChatColors.Yellow}!deletenade <#>{ChatColors.Default} - Delete saved position");
        player.PrintToChat($" {ChatColors.Yellow}!replay <#>{ChatColors.Default} - Load and replay moevement of saved grenade");
        player.PrintToChat($" {ChatColors.Yellow}!noclip{ChatColors.Default} - Toggle fly mode");
        player.PrintToChat($" {ChatColors.Yellow}!god{ChatColors.Default} - Toggle invincibility");
        player.PrintToChat($" {ChatColors.Yellow}!clear{ChatColors.Default} - Clear all grenades/smokes");
        player.PrintToChat($" {ChatColors.Yellow}!impacts{ChatColors.Default} - Toggle bullet impacts");
        player.PrintToChat($" {ChatColors.Yellow}!go <x, y, z>{ChatColors.Default} - Teleport to coordinates");
        player.PrintToChat($" {ChatColors.Yellow}!tspawn [#]{ChatColors.Default} / {ChatColors.Yellow}!ctspawn [#]{ChatColors.Default} - Cycle or jump to spawn");
		player.PrintToChat($" {ChatColors.Yellow}!restart{ChatColors.Default} - Restart round");
        player.PrintToChat($" {ChatColors.Yellow}!map <mapname>{ChatColors.Default} - Change map");
        player.PrintToChat($" {ChatColors.Yellow}!noflash{ChatColors.Default} - Toggle flash immunity");
		player.PrintToChat($" {ChatColors.Yellow}!damage{ChatColors.Default} / {ChatColors.Yellow}!stats{ChatColors.Default} - Show grenade damage stats");
		player.PrintToChat($" {ChatColors.Yellow}!clearstats{ChatColors.Default} - Clear damage stats");
        player.PrintToChat($" {ChatColors.Green}═══════ BOT COMMANDS ═══════");
        player.PrintToChat($" {ChatColors.Yellow}!bot{ChatColors.Default} - Add a bot");
        player.PrintToChat($" {ChatColors.Yellow}!ctbot{ChatColors.Default} / {ChatColors.Yellow}!tbot{ChatColors.Default} - Add CT/T bot");
        player.PrintToChat($" {ChatColors.Yellow}!nobot{ChatColors.Default} - Remove bot you're looking at");
        player.PrintToChat($" {ChatColors.Yellow}!kickbots{ChatColors.Default} - Remove all bots");
        player.PrintToChat($" {ChatColors.Yellow}!place{ChatColors.Default} - Place bot at your position");
		player.PrintToChat($" {ChatColors.Yellow}!ctplace{ChatColors.Default} - Place CT bot (spawns if none exist)");
		player.PrintToChat($" {ChatColors.Yellow}!tplace{ChatColors.Default} - Place T bot (spawns if none exist)");
        player.PrintToChat($" {ChatColors.Yellow}!stand{ChatColors.Default} / {ChatColors.Yellow}!boost{ChatColors.Default} - Make bot stand/crouch");
        player.PrintToChat($" {ChatColors.Yellow}!jump{ChatColors.Default} - Make bot jump");
    }

    [ConsoleCommand("css_back", "Return to last throw position")]
    public void OnBackCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var steamId = player.SteamID;
        if (_lastPosition.ContainsKey(steamId) && _lastAngle.ContainsKey(steamId))
        {
            TeleportPlayer(player, _lastPosition[steamId], _lastAngle[steamId]);
            
            if (_lastGrenadeType.ContainsKey(steamId))
            {
                AddTimer(0.1f, () =>
                {
                    if (player.IsValid && player.PawnIsAlive)
                    {
                        var grenadeType = _lastGrenadeType[steamId];
                        player.GiveNamedItem(grenadeType);
                        
                        AddTimer(0.1f, () =>
                        {
                            player.ExecuteClientCommand($"slot{GetGrenadeSlot(grenadeType)}");
                        });
                    }
                });
            }
            
            player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Returned to throw position!");
        }
        else
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No previous position saved!");
        }
    }

    [ConsoleCommand("css_rethrow", "Rethrow last grenade using sv_rethrow")]
    public void OnRethrowCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        Server.ExecuteCommand("sv_rethrow_last_grenade");
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Rethrowing last grenade!");
    }

	[ConsoleCommand("css_record", "Start recording a grenade lineup")]
	[ConsoleCommand("css_rec", "Start recording a grenade lineup")]
	public void OnRecordCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		var steamId = player.SteamID;
		
		// Check if already recording
		if (_activeRecordings.ContainsKey(steamId))
		{
			player.PrintToChat($" {ChatColors.Red}[Record]{ChatColors.Default} Already recording! Throw grenade or !cancel");
			return;
		}
		
		// Load existing nades from file
		LoadNadesFromFile(steamId);
		
		// Show how many nades saved
		var existingCount = _savedNades.ContainsKey(steamId) ? _savedNades[steamId].Count : 0;
		if (existingCount > 0)
		{
			player.PrintToChat($" {ChatColors.Yellow}[Record]{ChatColors.Default} You have {existingCount} saved lineups.");
		}
		
		// Save angles
		var lockedAngle = new QAngle(
			playerPawn.EyeAngles.X, 
			playerPawn.EyeAngles.Y, 
			playerPawn.EyeAngles.Z
		);
		
		var recording = new GrenadeRecording
		{
			StartPosition = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z),
			StartAngle = lockedAngle,
			RecordStartTime = Server.CurrentTime,
			Frames = new List<MovementFrame>()
		};
		
		_activeRecordings[steamId] = recording;
		
		player.PrintToChat($" {ChatColors.Green}[Record]{ChatColors.Default} Recording started!");
		player.PrintToChat($" {ChatColors.Yellow}Pre-aim locked:{ChatColors.Default} Pitch {lockedAngle.X:F1}° Yaw {lockedAngle.Y:F1}°");
		player.PrintToChat($" {ChatColors.Grey}Starting crosshair position saved. Throw when ready...");
	}

	[ConsoleCommand("css_cancelrec", "Cancel current recording")]
	[ConsoleCommand("css_cancel", "Cancel current recording")]
	public void OnCancelRecordCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		
		var steamId = player.SteamID;
		
		if (_activeRecordings.Remove(steamId) || _pendingSaves.Remove(steamId))
		{
			player.PrintToChat($" {ChatColors.Red}[Record]{ChatColors.Default} Recording cancelled.");
		}
		else
		{
			player.PrintToChat($" {ChatColors.Red}[Record]{ChatColors.Default} No active recording!");
		}
	}

	[ConsoleCommand("css_saverec", "Save the recorded grenade")]
	[ConsoleCommand("css_save", "Save the recorded grenade")]
	public void OnSaveRecordingCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		
		var steamId = player.SteamID;
		
		if (!_pendingSaves.ContainsKey(steamId))
		{
			player.PrintToChat($" {ChatColors.Red}[Record]{ChatColors.Default} No recording to save!");
			return;
		}
		
		// LOAD EXISTING NADES FIRST
		LoadNadesFromFile(steamId);
		
		var recording = _pendingSaves[steamId];
		var suggestedName = recording.DetectedTechnique;
		
		var customName = command.ArgString.Trim();
		var finalName = string.IsNullOrEmpty(customName) ? suggestedName : $"{suggestedName} - {customName}";
		
		// Initialize if needed
		if (!_savedNades.ContainsKey(steamId))
			_savedNades[steamId] = new List<GrenadePosition>();
		
		// Add to existing list
		_savedNades[steamId].Add(new GrenadePosition
		{
			Name = finalName,
			X = recording.StartPosition.X,
			Y = recording.StartPosition.Y,
			Z = recording.StartPosition.Z,
			Pitch = recording.StartAngle.X,
			Yaw = recording.StartAngle.Y,
			Roll = recording.StartAngle.Z,
			GrenadeType = recording.GrenadeType,
			MovementFrames = recording.Frames,
			Technique = recording.DetectedTechnique,
			HasRecording = true,
			ThrowX = recording.ThrowPosition.X,
			ThrowY = recording.ThrowPosition.Y,
			ThrowZ = recording.ThrowPosition.Z,
			ThrowPitch = recording.ThrowAngle.X,
			ThrowYaw = recording.ThrowAngle.Y,
			ThrowRoll = recording.ThrowAngle.Z
		});
		
		SaveNadesToFile(steamId);
		_pendingSaves.Remove(steamId);
		
		player.PrintToChat($" {ChatColors.Green}[Record]{ChatColors.Default} ✅ Saved '{finalName}'!");
		player.PrintToChat($" {ChatColors.Grey}Total lineups: {_savedNades[steamId].Count} | Technique: {recording.DetectedTechnique}");
	}

	[ConsoleCommand("css_retry", "Retry the grenade throw")]
	public void OnRetryCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		
		var steamId = player.SteamID;
		
		if (!_pendingSaves.ContainsKey(steamId))
		{
			player.PrintToChat($" {ChatColors.Red}[Record]{ChatColors.Default} No recording to retry!");
			return;
		}
		
		var recording = _pendingSaves[steamId];
		
		// Teleport back to start position
		TeleportPlayer(player, recording.StartPosition, recording.StartAngle);
		
		// Give grenade
		AddTimer(0.1f, () =>
		{
			if (player.IsValid && player.PawnIsAlive)
			{
				player.GiveNamedItem(recording.GrenadeType);
				AddTimer(0.1f, () =>
				{
					player.ExecuteClientCommand($"slot{GetGrenadeSlot(recording.GrenadeType)}");
				});
			}
		});
		
		// Start new recording from same position
		_activeRecordings[steamId] = new GrenadeRecording
		{
			StartPosition = recording.StartPosition,
			StartAngle = recording.StartAngle,
			RecordStartTime = Server.CurrentTime,
			Frames = new List<MovementFrame>()
		};
		
		_pendingSaves.Remove(steamId);
		
		player.PrintToChat($" {ChatColors.Yellow}[Record]{ChatColors.Default} Retrying from start position...");
	}

    [ConsoleCommand("css_listnades", "List saved positions")]
	public void OnListNadesCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		var steamId = player.SteamID;
		LoadNadesFromFile(steamId);
		if (!_savedNades.ContainsKey(steamId) || _savedNades[steamId].Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No saved positions!");
			return;
		}
		player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Your saved lineups:");
		for (int i = 0; i < _savedNades[steamId].Count; i++)
		{
			var nade = _savedNades[steamId][i];
			var recordLabel = nade.HasRecording ? "[REC]" : "[POS]";
			var technique = nade.HasRecording ? $" [{nade.Technique}]" : "";
			player.PrintToChat($" {recordLabel} {ChatColors.Yellow}#{i + 1}{ChatColors.Default} {nade.Name}{technique} {ChatColors.Grey}[{GetGrenadeName(nade.GrenadeType)}]");
		}
		player.PrintToChat($" {ChatColors.Grey}Use !loadnade <#>");
		player.PrintToChat($" {ChatColors.Grey}[REC] = Has recording | [POS] = Static position");
	}

	[ConsoleCommand("css_loadnade", "Load saved position")]
	public void OnLoadNadeCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		var steamId = player.SteamID;
		LoadNadesFromFile(steamId);
		if (!_savedNades.ContainsKey(steamId) || _savedNades[steamId].Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No saved positions!");
			return;
		}
		if (!int.TryParse(command.ArgString, out int index) || index < 1 || index > _savedNades[steamId].Count)
		{
			player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Invalid index! Use !listnades");
			return;
		}
		var nade = _savedNades[steamId][index - 1];
		var pos = new Vector(nade.X, nade.Y, nade.Z);
		var angle = new QAngle(nade.Pitch, nade.Yaw, nade.Roll);
		
		TeleportPlayer(player, pos, angle);
		
		AddTimer(0.1f, () =>
		{
			if (player.IsValid && player.PawnIsAlive)
			{
				player.GiveNamedItem(nade.GrenadeType);
				
				AddTimer(0.1f, () =>
				{
					player.ExecuteClientCommand($"slot{GetGrenadeSlot(nade.GrenadeType)}");
				});
				
				var recordLabel = nade.HasRecording ? "[REC]" : "[POS]";
				player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} {recordLabel} Loaded '{nade.Name}' with {GetGrenadeName(nade.GrenadeType)}");
			}
		});
	}

    [ConsoleCommand("css_deletenade", "Delete saved position")]
    [ConsoleCommand("css_delnade", "Delete saved position")]
    public void OnDeleteNadeCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var steamId = player.SteamID;
        LoadNadesFromFile(steamId);

        if (!_savedNades.ContainsKey(steamId) || _savedNades[steamId].Count == 0)
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No saved positions!");
            return;
        }

        if (!int.TryParse(command.ArgString, out int index) || index < 1 || index > _savedNades[steamId].Count)
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Invalid index!");
            return;
        }

        var nade = _savedNades[steamId][index - 1];
        _savedNades[steamId].RemoveAt(index - 1);
        SaveNadesToFile(steamId);
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Deleted '{nade.Name}'");
    }

    [ConsoleCommand("css_noclip", "Toggle noclip")]
    public void OnNoclipCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var pawn = player.PlayerPawn.Value;
        if (pawn == null) return;

        if (pawn.MoveType == MoveType_t.MOVETYPE_NOCLIP)
        {
            pawn.MoveType = MoveType_t.MOVETYPE_WALK;
            Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Noclip OFF");
        }
        else
        {
            pawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
            Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
            player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Noclip ON");
        }
    }

    [ConsoleCommand("css_god", "Toggle god mode")]
    public void OnGodCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var steamId = player.SteamID;
        _godMode[steamId] = !_godMode.GetValueOrDefault(steamId, false);

        if (_godMode[steamId])
        {
            player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} God mode ON");
        }
        else
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} God mode OFF");
        }
    }

    [ConsoleCommand("css_clear", "Clear grenades and effects")]
    public void OnClearCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var smokes = Utilities.FindAllEntitiesByDesignerName<CSmokeGrenadeProjectile>("smokegrenade_projectile");
        foreach (var smoke in smokes) smoke.Remove();

        var heGrenades = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("hegrenade_projectile");
        foreach (var he in heGrenades) he.Remove();

        var flashbangs = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("flashbang_projectile");
        foreach (var flash in flashbangs) flash.Remove();

        var molotovs = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("molotov_projectile");
        foreach (var molotov in molotovs) molotov.Remove();

        var incendiaries = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("incgrenade_projectile");
        foreach (var incendiary in incendiaries) incendiary.Remove();

        var decoys = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("decoy_projectile");
        foreach (var decoy in decoys) decoy.Remove();

        var fires = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("inferno");
        foreach (var fire in fires) fire.Remove();

        var particles = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>("info_particle_system");
        foreach (var particle in particles) particle.Remove();

        Server.ExecuteCommand("r_cleardecals");
        
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Cleared all grenades and effects!");
    }

    [ConsoleCommand("css_impacts", "Toggle bullet impacts")]
    public void OnImpactsCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        _impactsEnabled = !_impactsEnabled;

        if (_impactsEnabled)
        {
            Server.ExecuteCommand("sv_showimpacts 1");
            Server.ExecuteCommand("sv_showimpacts_time 10");
            player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Bullet impacts ON");
        }
        else
        {
            Server.ExecuteCommand("sv_showimpacts 0");
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Bullet impacts OFF");
        }
    }

    [ConsoleCommand("css_go", "Teleport to coordinates")]
    [ConsoleCommand("css_goto", "Teleport to coordinates")]
    public void OnGoCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var args = command.ArgString.Trim();
        if (string.IsNullOrEmpty(args))
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Usage: !go <x, y, z> or !go x y z");
            player.PrintToChat($" {ChatColors.Grey}Example: !go -1208, -1327, -167");
            return;
        }

        args = args.Replace("[", "").Replace("]", "").Replace(",", " ");
        var parts = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 3)
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Invalid format! Use: !go x y z");
            return;
        }

        if (!float.TryParse(parts[0], out float x) || 
            !float.TryParse(parts[1], out float y) || 
            !float.TryParse(parts[2], out float z))
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Invalid coordinates!");
            return;
        }

        var pos = new Vector(x, y, z);
        var pawn = player.PlayerPawn.Value;
        if (pawn == null) return;

        TeleportPlayer(player, pos, pawn.EyeAngles);
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Teleported to [{x:F0}, {y:F0}, {z:F0}]");
    }

    [ConsoleCommand("css_tspawn", "Browse T spawn points")]
    public void OnTSpawnCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;
        
        var arg = command.ArgString.Trim();
        int? targetIndex = null;
        
        if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int parsed))
        {
            targetIndex = parsed - 1;
        }
        
        BrowseSpawns(player, CsTeam.Terrorist, targetIndex);
    }

    [ConsoleCommand("css_ctspawn", "Browse CT spawn points")]
    public void OnCTSpawnCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;
        
        var arg = command.ArgString.Trim();
        int? targetIndex = null;
        
        if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out int parsed))
        {
            targetIndex = parsed - 1;
        }
        
        BrowseSpawns(player, CsTeam.CounterTerrorist, targetIndex);
    }

    [ConsoleCommand("css_map", "Change map")]
	public void OnMapCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null) return;
		
		try
		{
			var mapInput = command.ArgString.Trim();
			
			if (string.IsNullOrEmpty(mapInput))
			{
				player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Usage: !map <mapname>");
				player.PrintToChat($" {ChatColors.Grey}Examples: dust2, mirage, cs_italy");
				return;
			}
			
			var mapName = mapInput.ToLower();
			
			// Auto-add de_ prefix if no prefix exists
			if (!mapName.Contains("_"))
			{
				mapName = "de_" + mapName;
			}
			
			Console.WriteLine($"[PracticeMode] Map change requested: {mapName}");
			
			Server.PrintToChatAll($" {ChatColors.Green}[Practice]{ChatColors.Default} Changing to {mapName}...");
			
			// Comprehensive cleanup
			try
			{
				Console.WriteLine($"[PracticeMode] Killing timers...");
				_tickTimer?.Kill();
				_tickTimer = null;
				
				_flashCheckTimer?.Kill();
				_flashCheckTimer = null;
				
				_runBoostTimer?.Kill();
				_runBoostTimer = null;
				
				_botCrouchTimer?.Kill();
				_botCrouchTimer = null;
				
				Console.WriteLine($"[PracticeMode] Timers killed");
				
				if (_practiceMode)
				{
					Console.WriteLine($"[PracticeMode] Disabling practice mode...");
					DisablePracticeMode();
					_practiceMode = false;
				}
				
				Console.WriteLine($"[PracticeMode] Cleanup complete");
			}
			catch (Exception cleanupEx)
			{
				Console.WriteLine($"[PracticeMode] Cleanup error: {cleanupEx.Message}");
			}
			
			// Schedule map change
			AddTimer(1.0f, () =>
			{
				try
				{
					Console.WriteLine($"[PracticeMode] Executing map change to: {mapName}");
					Server.ExecuteCommand($"map {mapName}");
				}
				catch (Exception mapEx)
				{
					Console.WriteLine($"[PracticeMode] Map change execution error: {mapEx.Message}");
				}
			});
			
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[PracticeMode] Map command error: {ex.Message}");
			Console.WriteLine($"[PracticeMode] Stack trace: {ex.StackTrace}");
			player?.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Error changing map!");
		}
	}
	
	[ConsoleCommand("css_restart", "Restart the round")]
	[ConsoleCommand("css_reset", "Restart the round")]
	public void OnRestartCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;

		Server.ExecuteCommand("mp_restartgame 3");
		player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Restarting round in 3 seconds...");
	}

    [ConsoleCommand("css_noflash", "Toggle flash immunity")]
    [ConsoleCommand("css_flash", "Toggle flash immunity")]
    public void OnNoFlashCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var steamId = player.SteamID;
        _noFlash[steamId] = !_noFlash.GetValueOrDefault(steamId, false);

        if (_noFlash[steamId])
        {
            player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Flash immunity ON - You won't be flashed!");
        }
        else
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Flash immunity OFF");
        }
    }

    [ConsoleCommand("css_bot", "Add a bot")]
    public void OnBotCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        // Get current bot list
        var existingBotIds = Utilities.GetPlayers()
            .Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
            .Select(p => p.UserId!.Value)
            .ToList();

        // Count bots on each team
        var ctBots = Utilities.GetPlayers().Count(p => p.IsBot && p.Team == CsTeam.CounterTerrorist);
        var tBots = Utilities.GetPlayers().Count(p => p.IsBot && p.Team == CsTeam.Terrorist);

        // Add to team with fewer bots
        if (ctBots <= tBots)
        {
            Server.ExecuteCommand("bot_add_ct");
        }
        else
        {
            Server.ExecuteCommand("bot_add_t");
        }
        
        AddTimer(1.0f, () =>
        {
            Server.ExecuteCommand("bot_stop 1");
            Server.ExecuteCommand("bot_freeze 1");
            Server.ExecuteCommand("bot_dont_shoot 1");
            Server.ExecuteCommand("bot_zombie 1");
            
            // Find new bots that weren't in the original list
            var allBots = Utilities.GetPlayers()
                .Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
                .ToList();
            
            var newBots = allBots.Where(b => !existingBotIds.Contains(b.UserId!.Value)).ToList();
            
            // Keep only the first new bot, remove all others
            if (newBots.Count > 1)
            {
                for (int i = 1; i < newBots.Count; i++)
                {
                    Server.ExecuteCommand($"kickid {newBots[i].UserId!.Value}");
                }
            }
            
            // Respawn the bot we're keeping if it's dead
            if (newBots.Count > 0 && !newBots[0].PawnIsAlive)
            {
                newBots[0].Respawn();
            }
        });
        
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Practice dummy added!");
    }

    [ConsoleCommand("css_ctbot", "Add a CT bot")]
    public void OnCTBotCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var existingBotIds = Utilities.GetPlayers()
            .Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
            .Select(p => p.UserId!.Value)
            .ToList();

        Server.ExecuteCommand("bot_add_ct");
        
        AddTimer(1.0f, () =>
        {
            Server.ExecuteCommand("bot_stop 1");
            Server.ExecuteCommand("bot_freeze 1");
            Server.ExecuteCommand("bot_dont_shoot 1");
            Server.ExecuteCommand("bot_zombie 1");
            
            var allBots = Utilities.GetPlayers()
                .Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
                .ToList();
            
            var newBots = allBots.Where(b => !existingBotIds.Contains(b.UserId!.Value)).ToList();
            
            // Keep first CT bot, remove others
            var newCTBot = newBots.FirstOrDefault(b => b.Team == CsTeam.CounterTerrorist);
            var botsToRemove = newBots.Where(b => b.UserId!.Value != newCTBot?.UserId).ToList();
            
            foreach (var bot in botsToRemove)
            {
                Server.ExecuteCommand($"kickid {bot.UserId!.Value}");
            }
            
            if (newCTBot != null && !newCTBot.PawnIsAlive)
            {
                newCTBot.Respawn();
            }
        });
        
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} CT practice dummy added!");
    }

    [ConsoleCommand("css_tbot", "Add a T bot")]
	public void OnTBotCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		
		var existingBotIds = Utilities.GetPlayers()
			.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
			.Select(p => p.UserId!.Value)
			.ToList();
		
		Server.ExecuteCommand("bot_add_t");
		
		AddTimer(1.0f, () =>
		{
			Server.ExecuteCommand("bot_stop 1");
			Server.ExecuteCommand("bot_freeze 1");
			Server.ExecuteCommand("bot_dont_shoot 1");
			Server.ExecuteCommand("bot_zombie 1");
			
			var allBots = Utilities.GetPlayers()
				.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
				.ToList();
			
			var newBots = allBots.Where(b => !existingBotIds.Contains(b.UserId!.Value)).ToList();
			
			// Keep first T bot, remove others
			var newTBot = newBots.FirstOrDefault(b => b.Team == CsTeam.Terrorist);
			var botsToRemove = newBots.Where(b => b.UserId!.Value != newTBot?.UserId).ToList();
			
			foreach (var bot in botsToRemove)
			{
				Server.ExecuteCommand($"kickid {bot.UserId!.Value}");
			}
			
			if (newTBot != null && !newTBot.PawnIsAlive)
			{
				newTBot.Respawn();
			}
		});
		
		player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} T practice dummy added!");
	}

    [ConsoleCommand("css_nobot", "Remove bot you're looking at")]
    [ConsoleCommand("css_removebot", "Remove bot you're looking at")]
    public void OnNoBotCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var targetBot = GetPlayerLookingAt(player);
        
        if (targetBot != null && targetBot.IsBot && targetBot.IsValid && targetBot.UserId.HasValue)
        {
            var botName = targetBot.PlayerName;
            var userId = targetBot.UserId.Value;
            
            // Remove from tracking dictionaries
            _botCrouchState.Remove(botName);
            _botRespawnPosition.Remove(botName);
            _botRespawnAngle.Remove(botName);
            
            // Kick the specific bot
            Server.ExecuteCommand($"kickid {userId}");
            player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Removed bot: {botName}");
        }
        else
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Not looking at a bot! Look at the bot you want to remove.");
        }
    }

    [ConsoleCommand("css_kickbots", "Remove all bots")]
    [ConsoleCommand("css_nobots", "Remove all bots")]
    public void OnKickBotsCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        Server.ExecuteCommand("bot_kick");
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} All bots removed!");
    }

    [ConsoleCommand("css_place", "Place bot at your position")]
	[ConsoleCommand("css_placebot", "Place bot at your position")]
	public void OnPlaceBotCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		var bots = Utilities.GetPlayers()
			.Where(p => p.IsBot && p.IsValid && p.PawnIsAlive)
			.ToList();
		
		// If NO bots exist, spawn a teammate bot first
		if (bots.Count == 0)
		{
			var playerTeam = player.Team;
			
			if (playerTeam != CsTeam.CounterTerrorist && playerTeam != CsTeam.Terrorist)
			{
				player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Join a team first!");
				return;
			}
			
			// CAPTURE position and angle NOW before timer
			var capturedPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
			var capturedAngle = new QAngle(0, playerPawn.EyeAngles.Y, 0);
			
			player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Spawning teammate bot...");
			
			var existingBotIds = Utilities.GetPlayers()
				.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
				.Select(p => p.UserId!.Value)
				.ToList();
			
			if (playerTeam == CsTeam.CounterTerrorist)
			{
				Server.ExecuteCommand("bot_add_ct");
			}
			else
			{
				Server.ExecuteCommand("bot_add_t");
			}
			
			AddTimer(1.0f, () =>
			{
				Server.ExecuteCommand("bot_stop 1");
				Server.ExecuteCommand("bot_freeze 1");
				Server.ExecuteCommand("bot_dont_shoot 1");
				Server.ExecuteCommand("bot_zombie 1");
				
				var allBots = Utilities.GetPlayers()
					.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
					.ToList();
				
				var newBots = allBots.Where(b => !existingBotIds.Contains(b.UserId!.Value)).ToList();
				
				// Keep first bot on player's team
				var newTeammateBot = newBots.FirstOrDefault(b => b.Team == playerTeam);
				var botsToRemove = newBots.Where(b => b.UserId!.Value != newTeammateBot?.UserId).ToList();
				
				foreach (var bot in botsToRemove)
				{
					Server.ExecuteCommand($"kickid {bot.UserId!.Value}");
				}
				
				if (newTeammateBot != null)
				{
					if (!newTeammateBot.PawnIsAlive)
					{
						newTeammateBot.Respawn();
					}
					
					// Place the bot at captured position
					AddTimer(0.5f, () =>
					{
						if (newTeammateBot.IsValid && newTeammateBot.PawnIsAlive && newTeammateBot.PlayerPawn.Value != null)
						{
							var botPawn = newTeammateBot.PlayerPawn.Value;
							botPawn.Teleport(capturedPos, capturedAngle, new Vector(0, 0, 0));
							
							var botName = newTeammateBot.PlayerName;
							_botRespawnPosition[botName] = capturedPos;
							_botRespawnAngle[botName] = capturedAngle;
							
							player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed teammate bot!");
						}
					});
				}
				else
				{
					player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Failed to spawn bot! Try again.");
				}
			});
			
			return;
		}
		
		// Bots exist - use normal placement logic
		var steamId = player.SteamID;
		if (!_lastBotIndex.ContainsKey(steamId))
		{
			_lastBotIndex[steamId] = 0;
		}
		
		var botIndex = _lastBotIndex[steamId] % bots.Count;
		var targetBot = bots[botIndex];
		_lastBotIndex[steamId] = (botIndex + 1) % bots.Count;
		
		if (targetBot != null && targetBot.IsBot && targetBot.PawnIsAlive)
		{
			var pos = playerPawn.AbsOrigin;
			var eyeAngles = playerPawn.EyeAngles;
			var angle = new QAngle(0, eyeAngles.Y, 0);
			
			var botPawn = targetBot.PlayerPawn.Value;
			if (botPawn != null)
			{
				var isEnemy = (player.Team == CsTeam.CounterTerrorist && targetBot.Team == CsTeam.Terrorist) ||
							  (player.Team == CsTeam.Terrorist && targetBot.Team == CsTeam.CounterTerrorist);
				
				if (isEnemy)
				{
					playerPawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
					Schema.SetSchemaValue(playerPawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
				}
				
				AddTimer(0.1f, () =>
				{
					if (targetBot.IsValid && targetBot.PawnIsAlive && targetBot.PlayerPawn.Value != null)
					{
						var pawn = targetBot.PlayerPawn.Value;
						pawn.Teleport(pos, angle, new Vector(0, 0, 0));
						
						var botName = targetBot.PlayerName;
						_botRespawnPosition[botName] = new Vector(pos.X, pos.Y, pos.Z);
						_botRespawnAngle[botName] = new QAngle(0, eyeAngles.Y, 0);
					}
				});
				
				if (isEnemy)
				{
					AddTimer(3.0f, () =>
					{
						if (player.IsValid && player.PawnIsAlive && playerPawn.IsValid)
						{
							playerPawn.MoveType = MoveType_t.MOVETYPE_WALK;
							Schema.SetSchemaValue(playerPawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
						}
					});
					
					player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed enemy bot #{botIndex + 1}! (3s noclip)");
				}
				else
				{
					player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed teammate bot #{botIndex + 1}!");
				}
			}
		}
	}
	
	[ConsoleCommand("css_ctplace", "Place CT bot at your position")]
	public void OnCTPlaceBotCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		var ctBots = Utilities.GetPlayers()
			.Where(p => p.IsBot && p.IsValid && p.PawnIsAlive && p.Team == CsTeam.CounterTerrorist)
			.ToList();
		
		// If NO CT bots exist, spawn one first
		if (ctBots.Count == 0)
		{
			// CAPTURE position and angle NOW before timer
			var capturedPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
			var capturedAngle = new QAngle(0, playerPawn.EyeAngles.Y, 0);
			var capturedIsEnemy = player.Team == CsTeam.Terrorist;
			
			player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Spawning CT bot...");
			
			var existingBotIds = Utilities.GetPlayers()
				.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
				.Select(p => p.UserId!.Value)
				.ToList();
			
			Server.ExecuteCommand("bot_add_ct");
			
			AddTimer(1.0f, () =>
			{
				Server.ExecuteCommand("bot_stop 1");
				Server.ExecuteCommand("bot_freeze 1");
				Server.ExecuteCommand("bot_dont_shoot 1");
				Server.ExecuteCommand("bot_zombie 1");
				
				var allBots = Utilities.GetPlayers()
					.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
					.ToList();
				
				var newBots = allBots.Where(b => !existingBotIds.Contains(b.UserId!.Value)).ToList();
				
				// Keep first CT bot, remove others
				var newCTBot = newBots.FirstOrDefault(b => b.Team == CsTeam.CounterTerrorist);
				var botsToRemove = newBots.Where(b => b.UserId!.Value != newCTBot?.UserId).ToList();
				
				foreach (var bot in botsToRemove)
				{
					Server.ExecuteCommand($"kickid {bot.UserId!.Value}");
				}
				
				if (newCTBot != null)
				{
					// FORCE RESPAWN if bot is dead
					if (!newCTBot.PawnIsAlive)
					{
						newCTBot.Respawn();
					}
					
					// Place the bot at captured position after longer delay
					AddTimer(1.0f, () =>  // Increased from 0.5f to 1.0f
					{
						if (newCTBot.IsValid && newCTBot.PlayerPawn.Value != null)
						{
							// Force respawn again if still dead
							if (!newCTBot.PawnIsAlive)
							{
								newCTBot.Respawn();
								
								// Try one more time after respawn
								AddTimer(0.5f, () =>
								{
									if (newCTBot.IsValid && newCTBot.PawnIsAlive && newCTBot.PlayerPawn.Value != null)
									{
										var botPawn = newCTBot.PlayerPawn.Value;
										botPawn.Teleport(capturedPos, capturedAngle, new Vector(0, 0, 0));
										
										var botName = newCTBot.PlayerName;
										_botRespawnPosition[botName] = capturedPos;
										_botRespawnAngle[botName] = capturedAngle;
									}
								});
							}
							else
							{
								var botPawn = newCTBot.PlayerPawn.Value;
								botPawn.Teleport(capturedPos, capturedAngle, new Vector(0, 0, 0));
								
								var botName = newCTBot.PlayerName;
								_botRespawnPosition[botName] = capturedPos;
								_botRespawnAngle[botName] = capturedAngle;
							}
							
							// Apply noclip to player if enemy
							if (capturedIsEnemy && player.IsValid && player.PawnIsAlive && player.PlayerPawn.Value != null)
							{
								var pPawn = player.PlayerPawn.Value;
								pPawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
								Schema.SetSchemaValue(pPawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
								
								AddTimer(3.0f, () =>
								{
									if (player.IsValid && player.PawnIsAlive && player.PlayerPawn.Value != null)
									{
										var p = player.PlayerPawn.Value;
										p.MoveType = MoveType_t.MOVETYPE_WALK;
										Schema.SetSchemaValue(p.Handle, "CBaseEntity", "m_nActualMoveType", 2);
									}
								});
								
								player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed enemy CT bot! (3s noclip)");
							}
							else
							{
								player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed teammate CT bot!");
							}
						}
					});
				}
				else
				{
					player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Failed to spawn CT bot! Try again.");
				}
			});
			
			return;
		}
		
		// CT bots exist - place one
		PlaceSpecificTeamBot(player, CsTeam.CounterTerrorist);
	}
	
	[ConsoleCommand("css_tplace", "Place T bot at your position")]
	public void OnTPlaceBotCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		var tBots = Utilities.GetPlayers()
			.Where(p => p.IsBot && p.IsValid && p.PawnIsAlive && p.Team == CsTeam.Terrorist)
			.ToList();
		
		// If NO T bots exist, spawn one first
		if (tBots.Count == 0)
		{
			// capture position and angle before timer
			var capturedPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
			var capturedAngle = new QAngle(0, playerPawn.EyeAngles.Y, 0);
			var capturedIsEnemy = player.Team == CsTeam.CounterTerrorist;
			
			player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Spawning T bot...");
			
			var existingBotIds = Utilities.GetPlayers()
				.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
				.Select(p => p.UserId!.Value)
				.ToList();
			
			Server.ExecuteCommand("bot_add_t");
			
			AddTimer(1.0f, () =>
			{
				Server.ExecuteCommand("bot_stop 1");
				Server.ExecuteCommand("bot_freeze 1");
				Server.ExecuteCommand("bot_dont_shoot 1");
				Server.ExecuteCommand("bot_zombie 1");
				
				var allBots = Utilities.GetPlayers()
					.Where(p => p.IsBot && p.IsValid && p.UserId.HasValue)
					.ToList();
				
				var newBots = allBots.Where(b => !existingBotIds.Contains(b.UserId!.Value)).ToList();
				
				// Keep first T bot, remove others
				var newTBot = newBots.FirstOrDefault(b => b.Team == CsTeam.Terrorist);
				var botsToRemove = newBots.Where(b => b.UserId!.Value != newTBot?.UserId).ToList();
				
				foreach (var bot in botsToRemove)
				{
					Server.ExecuteCommand($"kickid {bot.UserId!.Value}");
				}
				
				if (newTBot != null)
				{
					// FORCE RESPAWN if bot is dead
					if (!newTBot.PawnIsAlive)
					{
						newTBot.Respawn();
					}
					
					// Place the bot at captured position after longer delay
					AddTimer(1.0f, () =>  // Increased from 0.5f to 1.0f
					{
						if (newTBot.IsValid && newTBot.PlayerPawn.Value != null)
						{
							// Force respawn again if still dead
							if (!newTBot.PawnIsAlive)
							{
								newTBot.Respawn();
								
								// Try one more time after respawn
								AddTimer(0.5f, () =>
								{
									if (newTBot.IsValid && newTBot.PawnIsAlive && newTBot.PlayerPawn.Value != null)
									{
										var botPawn = newTBot.PlayerPawn.Value;
										botPawn.Teleport(capturedPos, capturedAngle, new Vector(0, 0, 0));
										
										var botName = newTBot.PlayerName;
										_botRespawnPosition[botName] = capturedPos;
										_botRespawnAngle[botName] = capturedAngle;
									}
								});
							}
							else
							{
								var botPawn = newTBot.PlayerPawn.Value;
								botPawn.Teleport(capturedPos, capturedAngle, new Vector(0, 0, 0));
								
								var botName = newTBot.PlayerName;
								_botRespawnPosition[botName] = capturedPos;
								_botRespawnAngle[botName] = capturedAngle;
							}
							
							// Apply noclip to player if enemy
							if (capturedIsEnemy && player.IsValid && player.PawnIsAlive && player.PlayerPawn.Value != null)
							{
								var pPawn = player.PlayerPawn.Value;
								pPawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
								Schema.SetSchemaValue(pPawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
								
								AddTimer(3.0f, () =>
								{
									if (player.IsValid && player.PawnIsAlive && player.PlayerPawn.Value != null)
									{
										var p = player.PlayerPawn.Value;
										p.MoveType = MoveType_t.MOVETYPE_WALK;
										Schema.SetSchemaValue(p.Handle, "CBaseEntity", "m_nActualMoveType", 2);
									}
								});
								
								player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed enemy T bot! (3s noclip)");
							}
							else
							{
								player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed teammate T bot!");
							}
						}
					});
				}
				else
				{
					player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Failed to spawn T bot! Try again.");
				}
			});
			
			return;
		}
		
		// T bots exist - place one
		PlaceSpecificTeamBot(player, CsTeam.Terrorist);
	}

    [ConsoleCommand("css_boost", "Make all bots crouch for boost")]
	public void OnBoostCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;

		// Simple global command - affects ALL bots
		Server.ExecuteCommand("bot_crouch 1");
		
		player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} All bots are crouching!");
	}

	[ConsoleCommand("css_stand", "Make all bots stand")]
	public void OnStandCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;

		// Simple global command - affects ALL bots
		Server.ExecuteCommand("bot_crouch 0");
		
		player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} All bots are standing!");
	}

    [ConsoleCommand("css_jump", "Make bot jump")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnJumpCommand(CCSPlayerController player, CommandInfo command)
    {
        if (player == null || !_practiceMode) return;

        var targetBot = GetClosestBot(player);

        if (targetBot != null && targetBot.IsBot && targetBot.PawnIsAlive)
        {
            var botPawn = targetBot.PlayerPawn.Value;
            if (botPawn != null)
            {
                var currentPos = botPawn.AbsOrigin;
                var currentAngle = botPawn.AbsRotation;
                var jumpVelocity = new Vector(0, 0, 300.0f);
                
                botPawn.Teleport(currentPos, currentAngle, jumpVelocity);
                
                player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Bot {targetBot.PlayerName} jumped!");
            }
        }
        else
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No bot found!");
        }
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !_practiceMode) return HookResult.Continue;

		AddTimer(0.5f, () =>
		{
			if (player.IsValid && player.PawnIsAlive)
			{
				GiveAllGrenades(player);
				GiveFullMoney(player);
				
				// Give god mode to human players if they have it enabled
				if (!player.IsBot && _godMode.GetValueOrDefault(player.SteamID, false))
				{
					player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} God mode is ON");
				}
			}
		});

		return HookResult.Continue;
	}

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
	{
		var victim = @event.Userid;
		var attacker = @event.Attacker;
		
		if (victim == null || !_practiceMode) return HookResult.Continue;

		// Handle god mode 
		if (!victim.IsBot)
		{
			var steamId = victim.SteamID;
			if (_godMode.GetValueOrDefault(steamId, false))
			{
				AddTimer(0.01f, () =>
				{
					if (victim.IsValid && victim.PlayerPawn.IsValid && victim.PlayerPawn.Value != null)
					{
						var pawn = victim.PlayerPawn.Value;
						pawn.Health = 100;
						pawn.ArmorValue = 100;
					}
				});
			}
		}

		// Track grenade damage
		if (attacker != null && attacker.IsValid)
		{
			var attackerSteamId = attacker.SteamID;
			var weapon = @event.Weapon;
			
			if (IsGrenadeWeapon(weapon))
			{
				var damage = @event.DmgHealth;
				
				if (!_totalGrenadeDamage.ContainsKey(attackerSteamId))
				{
					_totalGrenadeDamage[attackerSteamId] = 0;
				}
				_totalGrenadeDamage[attackerSteamId] += damage;
				
				if (!_grenadeDamageByType.ContainsKey(attackerSteamId))
				{
					_grenadeDamageByType[attackerSteamId] = new Dictionary<string, int>();
				}
				
				var grenadeName = GetGrenadeNameFromWeapon(weapon);
				if (!_grenadeDamageByType[attackerSteamId].ContainsKey(grenadeName))
				{
					_grenadeDamageByType[attackerSteamId][grenadeName] = 0;
				}
				_grenadeDamageByType[attackerSteamId][grenadeName] += damage;
				
				// Show damage dealt
				var victimName = victim.IsBot ? $"Bot ({victim.PlayerName})" : victim.PlayerName;
				var healthRemaining = victim.PlayerPawn.Value?.Health ?? 0;
				attacker.PrintToChat($" {ChatColors.Yellow}[Damage]{ChatColors.Default} {grenadeName}: {ChatColors.Red}{damage} HP{ChatColors.Default} to {victimName} ({ChatColors.Red}{healthRemaining} HP{ChatColors.Default} remaining)");
			}
		}

		return HookResult.Continue;
	}

	private bool IsGrenadeWeapon(string weapon)
	{
		return weapon.Contains("hegrenade") || weapon.Contains("molotov") || 
			   weapon.Contains("incgrenade") || weapon.Contains("inferno");
	}

	private string GetGrenadeNameFromWeapon(string weapon)
	{
		if (weapon.Contains("hegrenade")) return "HE Grenade";
		if (weapon.Contains("molotov")) return "Molotov";
		if (weapon.Contains("incgrenade")) return "Incendiary";
		if (weapon.Contains("inferno")) return "Fire";
		return "Grenade";
	}

	[ConsoleCommand("css_damage", "Show grenade damage stats")]
	[ConsoleCommand("css_stats", "Show grenade damage stats")]
	public void OnDamageCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;

		var steamId = player.SteamID;
		
		if (!_totalGrenadeDamage.ContainsKey(steamId) || _totalGrenadeDamage[steamId] == 0)
		{
			player.PrintToChat($" {ChatColors.Yellow}[Stats]{ChatColors.Default} No grenade damage dealt yet.");
			return;
		}

		player.PrintToChat($" {ChatColors.Green}═══════ GRENADE DAMAGE STATS ═══════");
		player.PrintToChat($" {ChatColors.Yellow}Total Damage:{ChatColors.Default} {ChatColors.Red}{_totalGrenadeDamage[steamId]} HP");
		
		if (_grenadeDamageByType.ContainsKey(steamId))
		{
			foreach (var entry in _grenadeDamageByType[steamId])
			{
				player.PrintToChat($" {ChatColors.Yellow}{entry.Key}:{ChatColors.Default} {ChatColors.Red}{entry.Value} HP");
			}
		}
		
		player.PrintToChat($" {ChatColors.Grey}Use !clearstats to reset");
	}

	[ConsoleCommand("css_clearstats", "Clear damage stats")]
	public void OnClearStatsCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;

		var steamId = player.SteamID;
		_totalGrenadeDamage[steamId] = 0;
		if (_grenadeDamageByType.ContainsKey(steamId))
		{
			_grenadeDamageByType[steamId].Clear();
		}
		
		player.PrintToChat($" {ChatColors.Green}[Stats]{ChatColors.Default} Damage stats cleared!");
	}

    private HookResult OnPlayerBlind(EventPlayerBlind @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !_practiceMode) return HookResult.Continue;

        var steamId = player.SteamID;
        
        if (_noFlash.GetValueOrDefault(steamId, false))
        {
            // Immediately cancel flash effect
            if (player.PlayerPawn?.Value != null)
            {
                var pawn = player.PlayerPawn.Value;
                pawn.FlashDuration = 0.0f;
                pawn.FlashMaxAlpha = 0.0f;
            }
            
            // Also schedule additional cleanup in case it gets reset
            AddTimer(0.01f, () =>
            {
                if (player.IsValid && player.PlayerPawn.IsValid && player.PlayerPawn.Value != null)
                {
                    var pawn = player.PlayerPawn.Value;
                    pawn.FlashDuration = 0.0f;
                    pawn.FlashMaxAlpha = 0.0f;
                }
            });
            
            AddTimer(0.05f, () =>
            {
                if (player.IsValid && player.PlayerPawn.IsValid && player.PlayerPawn.Value != null)
                {
                    var pawn = player.PlayerPawn.Value;
                    pawn.FlashDuration = 0.0f;
                    pawn.FlashMaxAlpha = 0.0f;
                }
            });
        }

        return HookResult.Continue;
    }

    private HookResult OnGrenadeThrown(EventGrenadeThrown @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !_practiceMode) return HookResult.Continue;

		var steamId = player.SteamID;
		var pawn = player.PlayerPawn.Value;
		if (pawn == null) return HookResult.Continue;

		// Check if recording
		if (_activeRecordings.ContainsKey(steamId))
		{
			var recording = _activeRecordings[steamId];
    
			var throwPitch = Math.Max(-89f, Math.Min(89f, pawn.EyeAngles.X));
			
			recording.ThrowPosition = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z);
			recording.ThrowAngle = new QAngle(throwPitch, pawn.EyeAngles.Y, 0);
			
			var weapon = pawn.WeaponServices?.ActiveWeapon.Value;
			if (weapon != null)
			{
				recording.GrenadeType = weapon.DesignerName;
			}
			
			// Detect technique
			recording.DetectedTechnique = DetectThrowTechnique(recording);
			
			// STOP RECORDING NOW - don't record frames after throw
			_pendingSaves[steamId] = recording;
			_activeRecordings.Remove(steamId);
			
			// Prompt user
			player.PrintToChat($" {ChatColors.Green}═══════════════════════════════");
			player.PrintToChat($" {ChatColors.Green}[Record]{ChatColors.Default} Recording Complete!");
			player.PrintToChat($" {ChatColors.Yellow}Technique:{ChatColors.Default} {recording.DetectedTechnique}");
			player.PrintToChat($" {ChatColors.Yellow}Grenade:{ChatColors.Default} {GetGrenadeName(recording.GrenadeType)}");
			player.PrintToChat($" {ChatColors.Yellow}Duration:{ChatColors.Default} {recording.Frames.Count * 0.1f:F2}s ({recording.Frames.Count} frames)");
			player.PrintToChat($" {ChatColors.Yellow}Throw at:{ChatColors.Default} Frame {recording.Frames.Count}");
			player.PrintToChat($" {ChatColors.Green}═══════════════════════════════");
			player.PrintToChat($" {ChatColors.Green}!save {ChatColors.Default}/{ChatColors.Green} !saverec [name]{ChatColors.Default} - Save lineup");
			player.PrintToChat($" {ChatColors.Yellow}!retry{ChatColors.Default} - Try again from start");
			player.PrintToChat($" {ChatColors.Red}!cancel {ChatColors.Default}/{ChatColors.Red} !cancelrec{ChatColors.Default} - Cancel recording");
			
			return HookResult.Continue;
		}

		// Normal throw tracking (non-recording)
		_lastPosition[steamId] = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z);
		_lastAngle[steamId] = new QAngle(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z);
		
		var weapon2 = pawn.WeaponServices?.ActiveWeapon.Value;
		if (weapon2 != null)
		{
			_lastGrenadeType[steamId] = weapon2.DesignerName;
		}

		var throwData = new ThrowData
		{
			StartTime = Server.CurrentTime,
			StartPos = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z),
			GrenadeType = weapon2?.DesignerName ?? "unknown"
		};
		_activeThrows[steamId] = throwData;

		return HookResult.Continue;
	}
	
	[ConsoleCommand("css_replaynade", "Replay a recorded grenade lineup")]
	[ConsoleCommand("css_replay", "Replay a recorded grenade lineup")]
	public void OnReplayNadeCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;

		var steamId = player.SteamID;
		LoadNadesFromFile(steamId);

		if (!_savedNades.ContainsKey(steamId) || _savedNades[steamId].Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[Replay]{ChatColors.Default} No saved lineups!");
			return;
		}

		if (!int.TryParse(command.ArgString, out int index) || index < 1 || index > _savedNades[steamId].Count)
		{
			player.PrintToChat($" {ChatColors.Red}[Replay]{ChatColors.Default} Invalid index! Use !listnades");
			return;
		}

		var nade = _savedNades[steamId][index - 1];
		
		if (!nade.HasRecording || nade.MovementFrames.Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[Replay]{ChatColors.Default} '{nade.Name}' has no recording!");
			player.PrintToChat($" {ChatColors.Yellow}[Replay]{ChatColors.Default} Use !loadnade {index} to load it manually.");
			return;
		}

		player.PrintToChat($" {ChatColors.Green}[Replay]{ChatColors.Default} Playing '{nade.Name}' in cinematic mode...");
		player.PrintToChat($" {ChatColors.Grey}Sit back and watch! You'll be moved automatically.");
		player.PrintToChat($" {ChatColors.Yellow}Technique:{ChatColors.Default} {nade.Technique} | {ChatColors.Yellow}Duration:{ChatColors.Default} {nade.MovementFrames.Count * 0.05f:F1}s");
		
		StartPlayerCinematicReplay(player, nade);
	}

	private void StartPlayerCinematicReplay(CCSPlayerController player, GrenadePosition nade)
	{
		var steamId = player.SteamID;
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		// Save current position
		var originalPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
		var originalAngle = new QAngle(playerPawn.EyeAngles.X, playerPawn.EyeAngles.Y, playerPawn.EyeAngles.Z);
		
		// Enable noclip immediately
		playerPawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
		Schema.SetSchemaValue(playerPawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
		
		// Teleport to start
		var startPos = new Vector(nade.X, nade.Y, nade.Z);
		var startAngle = new QAngle(nade.Pitch, nade.Yaw, nade.Roll);
		
		var fakeJumpFrame = new MovementFrame
		{
			Time = 0,
			PosX = nade.X,
			PosY = nade.Y,
			PosZ = nade.Z + 50,  
			AngleX = nade.Pitch,
			AngleY = nade.Yaw,
			AngleZ = nade.Roll,
			Buttons = 1UL << 1, 
			IsJumping = true,
			IsCrouching = false,
			Velocity = 300
		};
		
		// Insert fake jump at beginning of frames
		var modifiedFrames = new List<MovementFrame> { fakeJumpFrame };
		modifiedFrames.AddRange(nade.MovementFrames);
		
		// Create temporary modified nade
		var modifiedNade = new GrenadePosition
		{
			Name = nade.Name,
			X = nade.X,
			Y = nade.Y,
			Z = nade.Z,
			Pitch = nade.Pitch,
			Yaw = nade.Yaw,
			Roll = nade.Roll,
			GrenadeType = nade.GrenadeType,
			MovementFrames = modifiedFrames,
			Technique = nade.Technique,
			HasRecording = nade.HasRecording,
			ThrowX = nade.ThrowX,
			ThrowY = nade.ThrowY,
			ThrowZ = nade.ThrowZ,
			ThrowPitch = nade.ThrowPitch,
			ThrowYaw = nade.ThrowYaw,
			ThrowRoll = nade.ThrowRoll
		};
		
		playerPawn.Teleport(startPos, startAngle, new Vector(0, 0, 0));
		
		// Create replay state with modified nade
		_activeReplays[steamId] = new ReplayState
		{
			Nade = modifiedNade,
			CurrentFrame = 0,
			StartTime = Server.CurrentTime,
			OriginalPosition = originalPos,
			OriginalAngle = originalAngle
		};
		
		// Give grenade
		AddTimer(0.3f, () =>
		{
			if (player.IsValid && player.PawnIsAlive)
			{
				player.GiveNamedItem(nade.GrenadeType);
				AddTimer(0.1f, () =>
				{
					if (player.IsValid && player.PawnIsAlive)
					{
						player.ExecuteClientCommand($"slot{GetGrenadeSlot(nade.GrenadeType)}");
					}
				});
			}
		});
	}

	[ConsoleCommand("css_stopreplay", "Stop current replay")]
	public void OnStopReplayCommand(CCSPlayerController player, CommandInfo command)
	{
		if (player == null || !_practiceMode) return;
		
		var steamId = player.SteamID;
		
		if (_activeReplays.ContainsKey(steamId))
		{
			var replayState = _activeReplays[steamId];
			
			// Restore movement
			var pawn = player.PlayerPawn.Value;
			if (pawn != null)
			{
				pawn.MoveType = MoveType_t.MOVETYPE_WALK;
				Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
			}
			
			// Return to original position
			TeleportPlayer(player, replayState.OriginalPosition, replayState.OriginalAngle);
			
			_activeReplays.Remove(steamId);
			player.PrintToChat($" {ChatColors.Red}[Replay]{ChatColors.Default} Replay stopped and cancelled.");
		}
		else
		{
			player.PrintToChat($" {ChatColors.Red}[Replay]{ChatColors.Default} No active replay!");
		}
	}

    private HookResult OnSmokeDetonate(EventSmokegrenadeDetonate @event, GameEventInfo info)
    {
        ShowGrenadeStats(@event.Userid, @event.X, @event.Y, @event.Z, "Smoke");
        return HookResult.Continue;
    }

    private HookResult OnHeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
    {
        ShowGrenadeStats(@event.Userid, @event.X, @event.Y, @event.Z, "HE Grenade");
        return HookResult.Continue;
    }

    private HookResult OnFlashDetonate(EventFlashbangDetonate @event, GameEventInfo info)
    {
        ShowGrenadeStats(@event.Userid, @event.X, @event.Y, @event.Z, "Flashbang");
        return HookResult.Continue;
    }

    private HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
    {
        ShowGrenadeStats(@event.Userid, @event.X, @event.Y, @event.Z, "Molotov");
        return HookResult.Continue;
    }

    private HookResult OnDecoyStarted(EventDecoyStarted @event, GameEventInfo info)
    {
        ShowGrenadeStats(@event.Userid, @event.X, @event.Y, @event.Z, "Decoy");
        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		var player = @event.Userid;
		if (player == null || !_practiceMode || !player.IsBot) return HookResult.Continue;

		var botName = player.PlayerName;
		var botPawn = player.PlayerPawn.Value;
		
		Vector respawnPos;
		QAngle respawnAngle;
		
		if (_botRespawnPosition.ContainsKey(botName) && _botRespawnAngle.ContainsKey(botName))
		{
			respawnPos = _botRespawnPosition[botName];
			respawnAngle = _botRespawnAngle[botName];
		}
		else if (botPawn != null)
		{
			respawnPos = new Vector(botPawn.AbsOrigin.X, botPawn.AbsOrigin.Y, botPawn.AbsOrigin.Z);
			respawnAngle = new QAngle(0, botPawn.AbsRotation.Y, 0);
			
			_botRespawnPosition[botName] = respawnPos;
			_botRespawnAngle[botName] = respawnAngle;
		}
		else
		{
			return HookResult.Continue;
		}
		
		AddTimer(3.0f, () =>
		{
			if (player.IsValid && !player.PawnIsAlive)
			{
				player.Respawn();
				
				AddTimer(0.5f, () =>
				{
					if (player.IsValid && player.PawnIsAlive)
					{
						var respawnedPawn = player.PlayerPawn.Value;
						if (respawnedPawn != null)
						{
							respawnedPawn.Teleport(respawnPos, respawnAngle, new Vector(0, 0, 0));
							// Let CS2 handle collision naturally - don't override
						}
					}
				});
			}
		});

		return HookResult.Continue;
	}
	
    private void ShowGrenadeStats(CCSPlayerController? player, float x, float y, float z, string grenadeType)
    {
        if (player == null || !_practiceMode) return;

        var steamId = player.SteamID;
        if (!_activeThrows.ContainsKey(steamId))
            return;

        var throwData = _activeThrows[steamId];
        var airtime = Server.CurrentTime - throwData.StartTime;
        
        var landPos = new Vector(x, y, z);
        var distance = CalculateDistance(throwData.StartPos, landPos);

        player.PrintToChat($" {ChatColors.Lime}⏱ {airtime:F2}s{ChatColors.Default} | {ChatColors.Yellow}{grenadeType}{ChatColors.Default} | Distance: {ChatColors.Yellow}{distance:F0}{ChatColors.Default} units");
        player.PrintToChat($" Landing: {ChatColors.Yellow}[{x:F0}, {y:F0}, {z:F0}]{ChatColors.Default} | Throw: {ChatColors.Yellow}[{throwData.StartPos.X:F0}, {throwData.StartPos.Y:F0}, {throwData.StartPos.Z:F0}]{ChatColors.Default}");

        _activeThrows.Remove(steamId);
    }

    private float CalculateDistance(Vector v1, Vector v2)
    {
        float dx = v2.X - v1.X;
        float dy = v2.Y - v1.Y;
        float dz = v2.Z - v1.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private void EnablePracticeMode()
	{
		Server.ExecuteCommand("rcon_password \"342987\"");
		Server.ExecuteCommand("sv_cheats 1");
		Server.ExecuteCommand("mp_warmup_end");
		Server.ExecuteCommand("mp_halftime 0");
		Server.ExecuteCommand("mp_match_end_restart 0");
		Server.ExecuteCommand("mp_freezetime 0");
		Server.ExecuteCommand("mp_round_restart_delay 0");
		Server.ExecuteCommand("mp_ignore_round_win_conditions 1");
		Server.ExecuteCommand("mp_roundtime 60");
		Server.ExecuteCommand("mp_roundtime_defuse 60");
		Server.ExecuteCommand("mp_roundtime_hostage 60");
		Server.ExecuteCommand("mp_maxmoney 65535");
		Server.ExecuteCommand("mp_startmoney 65535");
		Server.ExecuteCommand("mp_buytime 99999");
		Server.ExecuteCommand("mp_buy_anywhere 1");
		Server.ExecuteCommand("mp_buy_during_immunity 0");
		Server.ExecuteCommand("sv_infinite_ammo 1");
		Server.ExecuteCommand("ammo_grenade_limit_total 6");
		Server.ExecuteCommand("ammo_grenade_limit_flashbang 2");
		Server.ExecuteCommand("ammo_grenade_limit_default 1");
		Server.ExecuteCommand("sv_grenade_trajectory_prac_pipreview 1");
		Server.ExecuteCommand("sv_grenade_trajectory_prac_trailtime 10");
		Server.ExecuteCommand("sv_grenade_trajectory 1");
		Server.ExecuteCommand("sv_showimpacts 1");
		Server.ExecuteCommand("sv_showimpacts_time 10");
		Server.ExecuteCommand("mp_death_drop_gun 0");
		
		// These are already default but listing for clarity
		Server.ExecuteCommand("sv_accelerate 5.5");       
		Server.ExecuteCommand("sv_friction 5.2");         
		Server.ExecuteCommand("sv_stopspeed 80");         
		Server.ExecuteCommand("sv_maxvelocity 3500");
		
		// Collision settings
		Server.ExecuteCommand("mp_solid_teammates 2");
		Server.ExecuteCommand("mp_enemies_are_teammates 0");
		
		// Bot settings
		Server.ExecuteCommand("bot_mimic 0");
		Server.ExecuteCommand("bot_freeze 1");
		
		// Friendly fire settings
		Server.ExecuteCommand("mp_friendlyfire 1");
		Server.ExecuteCommand("ff_damage_reduction_bullets 1");
		Server.ExecuteCommand("ff_damage_reduction_grenade 1");
		Server.ExecuteCommand("ff_damage_reduction_other 1");
		Server.ExecuteCommand("mp_autokick 0");
		Server.ExecuteCommand("mp_spawnprotectiontime 0");
		
		// Disable auto-respawn and auto-balance
		Server.ExecuteCommand("mp_respawn_on_death_ct 0");
		Server.ExecuteCommand("mp_respawn_on_death_t 0");
		Server.ExecuteCommand("mp_autoteambalance 0");
		Server.ExecuteCommand("mp_limitteams 0");
		
		// Bot settings - make them practice dummies
		Server.ExecuteCommand("bot_kick");
		Server.ExecuteCommand("bot_stop 1");
		Server.ExecuteCommand("bot_freeze 1");
		Server.ExecuteCommand("bot_dont_shoot 1");
		Server.ExecuteCommand("bot_zombie 1");
		Server.ExecuteCommand("bot_difficulty 0");
		Server.ExecuteCommand("bot_quota_mode normal");
		Server.ExecuteCommand("bot_join_after_player 0");
		Server.ExecuteCommand("bot_auto_vacate 0");
		Server.ExecuteCommand("bot_quota 0");
		Server.ExecuteCommand("bot_chatter off");
		Server.ExecuteCommand("bot_allow_rogues 1");
		
		Server.ExecuteCommand("mp_restartgame 1");
		
		Console.WriteLine("[PracticeMode] Practice mode enabled!");
	}

    private void DisablePracticeMode()
    {
        Server.ExecuteCommand("sv_cheats 0");
        Server.ExecuteCommand("sv_grenade_trajectory 0");
        Server.ExecuteCommand("sv_infinite_ammo 0");
        Server.ExecuteCommand("mp_ignore_round_win_conditions 0");
        Server.ExecuteCommand("sv_showimpacts 0");
        Server.ExecuteCommand("bot_stop 0");
        Server.ExecuteCommand("bot_freeze 0");
        Server.ExecuteCommand("bot_dont_shoot 0");
        Server.ExecuteCommand("bot_zombie 0");
        Server.ExecuteCommand("mp_restartgame 1");
        
        Console.WriteLine("[PracticeMode] Practice mode disabled!");
    }

    private void GiveAllGrenades(CCSPlayerController player)
    {
        if (!player.IsValid || !player.PawnIsAlive) return;

        player.GiveNamedItem("weapon_knife");
        player.GiveNamedItem("weapon_hegrenade");
        player.GiveNamedItem("weapon_flashbang");
        player.GiveNamedItem("weapon_flashbang");
        player.GiveNamedItem("weapon_smokegrenade");
        player.GiveNamedItem("weapon_molotov");
        player.GiveNamedItem("weapon_incgrenade");
        player.GiveNamedItem("weapon_decoy");
    }

    private void GiveGrenadesToAll()
    {
        if (!_practiceMode) return;

        var players = Utilities.GetPlayers().Where(p => p.IsValid && p.PawnIsAlive);
        foreach (var player in players)
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null) continue;

            var weapons = pawn.WeaponServices?.MyWeapons;
            if (weapons == null) continue;

            bool hasHE = false, hasSmoke = false, hasFlash = false, hasMolotov = false, hasDecoy = false;

            foreach (var weapon in weapons)
            {
                if (weapon?.Value?.DesignerName == null) continue;
                var name = weapon.Value.DesignerName;
                
                if (name.Contains("hegrenade")) hasHE = true;
                if (name.Contains("smokegrenade")) hasSmoke = true;
                if (name.Contains("flashbang")) hasFlash = true;
                if (name.Contains("molotov") || name.Contains("incgrenade")) hasMolotov = true;
                if (name.Contains("decoy")) hasDecoy = true;
            }

            if (!hasHE) player.GiveNamedItem("weapon_hegrenade");
            if (!hasSmoke) player.GiveNamedItem("weapon_smokegrenade");
            if (!hasFlash)
            {
                player.GiveNamedItem("weapon_flashbang");
                player.GiveNamedItem("weapon_flashbang");
            }
            if (!hasMolotov)
            {
                player.GiveNamedItem("weapon_molotov");
                player.GiveNamedItem("weapon_incgrenade");
            }
            if (!hasDecoy) player.GiveNamedItem("weapon_decoy");
        }
    }

    private void CheckFlashImmunity()
	{
		if (!_practiceMode) return;
		
		var players = Utilities.GetPlayers().Where(p => p.IsValid && p.PawnIsAlive);
		
		foreach (var player in players)
		{
			var pawn = player.PlayerPawn.Value;
			if (pawn == null) continue;
			
			var steamId = player.SteamID;
			
			// Flash immunity check
			if (_noFlash.GetValueOrDefault(steamId, false))
			{
				if (pawn.FlashDuration > 0.0f || pawn.FlashMaxAlpha > 0.0f)
				{
					pawn.FlashDuration = 0.0f;
					pawn.FlashMaxAlpha = 0.0f;
				}
			}
		}
	}
	
	private void RecordMovement()
	{
		if (!_practiceMode) return;
		
		foreach (var kvp in _activeRecordings.ToList())
		{
			var steamId = kvp.Key;
			var recording = kvp.Value;
			
			var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID == steamId);
			if (player == null || !player.IsValid || !player.PawnIsAlive) continue;
			
			var pawn = player.PlayerPawn.Value;
			if (pawn == null) continue;
			
			var velocity = pawn.AbsVelocity;
			var speed = (float)Math.Sqrt(velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z);
			
			// Get buttons
			ulong buttons = 0;
			if (pawn.MovementServices != null && pawn.MovementServices.Buttons != null)
			{
				buttons = pawn.MovementServices.Buttons.ButtonStates[0];
			}
			
			// Check crouch multiple ways
			const ulong IN_DUCK = 1UL << 2;
			bool crouchFlag = (pawn.Flags & (uint)PlayerFlags.FL_DUCKING) != 0;
			bool crouchButton = (buttons & IN_DUCK) != 0;
			bool isCrouching = crouchFlag || crouchButton;
			
			var frame = new MovementFrame
			{
				Time = Server.CurrentTime - recording.RecordStartTime,
				PosX = pawn.AbsOrigin.X,
				PosY = pawn.AbsOrigin.Y,
				PosZ = pawn.AbsOrigin.Z,
				AngleX = pawn.EyeAngles.X,
				AngleY = pawn.EyeAngles.Y,           
				AngleZ = pawn.EyeAngles.Z,           
				Buttons = buttons,
				IsJumping = (pawn.Flags & (uint)PlayerFlags.FL_ONGROUND) == 0,
				IsCrouching = isCrouching,
				Velocity = speed
			};
			
			recording.Frames.Add(frame);
		}
	}

	private string DetectThrowTechnique(GrenadeRecording recording)
	{
		bool hasJump = false;
		bool hasCrouch = false;
		bool hasMovement = false;
		bool hasRun = false;
		
		// Count frames with different states to detect actions
		int runFrames = 0;
		int movementFrames = 0;
		int totalFrames = recording.Frames.Count;
		
		foreach (var frame in recording.Frames)
		{
			if (frame.IsJumping) hasJump = true;
			if (frame.IsCrouching) hasCrouch = true;
			
			// Count frames with significant velocity
			if (frame.Velocity > 200f) runFrames++;
			if (frame.Velocity > 100f) movementFrames++;
		}
		
		// Require velocity to count as "run" or "movement"
		// At least 30% of frames should have high velocity
		if (totalFrames > 0)
		{
			float runPercentage = (float)runFrames / totalFrames;
			float movementPercentage = (float)movementFrames / totalFrames;
			
			hasRun = runPercentage > 0.3f;  // 30% of frames running
			hasMovement = movementPercentage > 0.3f;  // 30% of frames moving
		}
		
		// Detect technique
		if (hasJump && hasRun && hasCrouch)
			return "Run Crouch Jump Throw";
		else if (hasJump && hasRun)
			return "Run Jump Throw";
		else if (hasJump && hasCrouch)
			return "Crouch Jump Throw";
		else if (hasJump && hasMovement)
			return "Walk Jump Throw";
		else if (hasJump)
			return "Jump Throw";
		else if (hasRun)
			return "Run Throw";
		else if (hasMovement)
			return "Walk Throw";
		else if (hasCrouch)
			return "Crouch Throw";
		else
			return "Standing Throw";
	}
	
	private void StartReplay(CCSPlayerController player, GrenadePosition nade)
	{
		var steamId = player.SteamID;
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		// Save current position to return to
		var originalPos = new Vector(playerPawn.AbsOrigin.X, playerPawn.AbsOrigin.Y, playerPawn.AbsOrigin.Z);
		var originalAngle = new QAngle(playerPawn.EyeAngles.X, playerPawn.EyeAngles.Y, playerPawn.EyeAngles.Z);
		
		// Start replay state
		_activeReplays[steamId] = new ReplayState
		{
			Nade = nade,
			CurrentFrame = 0,
			StartTime = Server.CurrentTime,
			OriginalPosition = originalPos,
			OriginalAngle = originalAngle
		};
		
		// Teleport to start position immediately
		var startPos = new Vector(nade.X, nade.Y, nade.Z);
		var startAngle = new QAngle(nade.Pitch, nade.Yaw, nade.Roll);
		TeleportPlayer(player, startPos, startAngle);
		
		// Give grenade
		AddTimer(0.2f, () =>
		{
			if (player.IsValid && player.PawnIsAlive)
			{
				player.GiveNamedItem(nade.GrenadeType);
				AddTimer(0.1f, () =>
				{
					if (player.IsValid && player.PawnIsAlive)
					{
						player.ExecuteClientCommand($"slot{GetGrenadeSlot(nade.GrenadeType)}");
					}
				});
			}
		});
	}

	private void UpdateReplays()
	{
		if (!_practiceMode) return;
		
		foreach (var kvp in _activeReplays.ToList())
		{
			var steamId = kvp.Key;
			var replayState = kvp.Value;
			
			var player = Utilities.GetPlayers().FirstOrDefault(p => p.SteamID == steamId);
			if (player == null || !player.IsValid || !player.PawnIsAlive)
			{
				_activeReplays.Remove(steamId);
				continue;
			}
			
			var pawn = player.PlayerPawn.Value;
			if (pawn == null)
			{
				_activeReplays.Remove(steamId);
				continue;
			}
			
			var frames = replayState.Nade.MovementFrames;
			
			// Check if replay finished
			if (replayState.CurrentFrame >= frames.Count)
			{
				player.PrintToChat($" {ChatColors.Yellow}[Replay]{ChatColors.Default} This is where you throw!");
				
				// Disable noclip
				pawn.MoveType = MoveType_t.MOVETYPE_WALK;
				Schema.SetSchemaValue(pawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
				
				player.PrintToCenter("=== THROW HERE! ===");
				
				AddTimer(0.3f, () => player.PrintToCenter("=== THROW HERE! ==="));
				AddTimer(0.6f, () => player.PrintToCenter("=== THROW HERE! ==="));
				AddTimer(0.9f, () => player.PrintToCenter("=== THROW HERE! ==="));
				AddTimer(1.2f, () => player.PrintToCenter("=== THROW HERE! ==="));
				
				// Return to lineup start
				AddTimer(2.0f, () =>
				{
					if (player.IsValid && player.PawnIsAlive)
					{
						var lineupStartPos = new Vector(replayState.Nade.X, replayState.Nade.Y, replayState.Nade.Z);
						var lineupPreAimAngle = new QAngle(replayState.Nade.Pitch, replayState.Nade.Yaw, replayState.Nade.Roll);
						
						// Just use TeleportPlayer
						TeleportPlayer(player, lineupStartPos, lineupPreAimAngle);
						
						// Give grenade
						AddTimer(0.1f, () =>
						{
							if (player.IsValid && player.PawnIsAlive)
							{
								player.GiveNamedItem(replayState.Nade.GrenadeType);
								
								AddTimer(0.1f, () =>
								{
									if (player.IsValid && player.PawnIsAlive)
									{
										player.ExecuteClientCommand($"slot{GetGrenadeSlot(replayState.Nade.GrenadeType)}");
									}
								});
								
								player.PrintToChat($" {ChatColors.Green}===============================");
								player.PrintToChat($" {ChatColors.Green}[Replay]{ChatColors.Default} Replay Complete!");
								player.PrintToChat($" {ChatColors.Yellow}Technique:{ChatColors.Default} {replayState.Nade.Technique}");
								player.PrintToChat($" {ChatColors.Grey}Try it yourself, then use {ChatColors.Green}!rethrow{ChatColors.Default} to practice!");
								player.PrintToChat($" {ChatColors.Green}===============================");
							}
						});
					}
				});
				
				_activeReplays.Remove(steamId);
				continue;
			}
			
			var frame = frames[replayState.CurrentFrame];
			
			// Apply frame position
			var framePos = new Vector(frame.PosX, frame.PosY, frame.PosZ);
			
			// If crouching, lower the camera position to simulate crouch view
			if (frame.IsCrouching)
			{
				framePos.Z -= 18.0f;
			}
			
			// Apply frame angles (no compensation)
			var frameAngle = new QAngle(frame.AngleX, frame.AngleY, frame.AngleZ);
			
			pawn.Teleport(framePos, frameAngle, new Vector(0, 0, 0));
			
			// Display keys
			if (replayState.CurrentFrame % 2 == 0)
			{
				DisplayReplayKeys(player, frame);
			}
			
			// Show progress every second
			if (replayState.CurrentFrame % 10 == 0)
			{
				var progress = (int)((float)replayState.CurrentFrame / frames.Count * 100);
				player.PrintToChat($" {ChatColors.Grey}[Replay] {progress}% complete...");
			}
			
			replayState.CurrentFrame++;
		}
	}

	private void DisplayReplayKeys(CCSPlayerController player, MovementFrame frame)
	{
		var keys = new List<string>();
		
		// Check buttons using the recorded button state
		var buttons = frame.Buttons;
		
		// Define button values
		const ulong IN_FORWARD = 1UL << 3;
		const ulong IN_BACK = 1UL << 4;
		const ulong IN_MOVELEFT = 1UL << 9;
		const ulong IN_MOVERIGHT = 1UL << 10;
		const ulong IN_JUMP = 1UL << 1;
		const ulong IN_DUCK = 1UL << 2;
		const ulong IN_SPEED = 1UL << 8;
		const ulong IN_ATTACK = 1UL << 0;
		const ulong IN_ATTACK2 = 1UL << 11;
		
		if ((buttons & IN_FORWARD) != 0) keys.Add("W");
		if ((buttons & IN_BACK) != 0) keys.Add("S");
		if ((buttons & IN_MOVELEFT) != 0) keys.Add("A");
		if ((buttons & IN_MOVERIGHT) != 0) keys.Add("D");
		if ((buttons & IN_JUMP) != 0 || frame.IsJumping) keys.Add("SPACE");
		if ((buttons & IN_DUCK) != 0 || frame.IsCrouching) keys.Add("CTRL");
		if ((buttons & IN_SPEED) != 0) keys.Add("SHIFT");
		if ((buttons & IN_ATTACK) != 0) keys.Add("MOUSE1");
		if ((buttons & IN_ATTACK2) != 0) keys.Add("MOUSE2");
		
		// Build display string
		var keyDisplay = keys.Count > 0 ? string.Join(" + ", keys) : "---";
		var velocityDisplay = $"{frame.Velocity:F0} u/s";
		var timeDisplay = $"{frame.Time:F2}s";
		
		// Display cinematic info
		player.PrintToCenter($"REPLAY [{timeDisplay}]\n{keyDisplay}\n{velocityDisplay}");
	}
	
    private CCSPlayerController? GetPlayerLookingAt(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return null;

        var eyePos = playerPawn.AbsOrigin + new Vector(0, 0, 64);
        var eyeAngle = playerPawn.EyeAngles;

        var pitch = eyeAngle.X * (Math.PI / 180.0);
        var yaw = eyeAngle.Y * (Math.PI / 180.0);

        var dirX = Math.Cos(pitch) * Math.Cos(yaw);
        var dirY = Math.Cos(pitch) * Math.Sin(yaw);
        var dirZ = -Math.Sin(pitch);

        var direction = new Vector((float)dirX, (float)dirY, (float)dirZ);

        CCSPlayerController? closestPlayer = null;
        float closestDist = float.MaxValue;
        float maxDistance = 5000.0f;

        var allPlayers = Utilities.GetPlayers().Where(p => 
            p.IsValid && 
            p.PawnIsAlive && 
            p.UserId != player.UserId
        );

        foreach (var targetPlayer in allPlayers)
        {
            var targetPawn = targetPlayer.PlayerPawn.Value;
            if (targetPawn == null) return null;

            var targetPos = targetPawn.AbsOrigin + new Vector(0, 0, 36);
            var toTarget = targetPos - eyePos;
            var distance = toTarget.Length();

            if (distance > maxDistance) continue;

            var toTargetNorm = new Vector(
                toTarget.X / distance,
                toTarget.Y / distance,
                toTarget.Z / distance
            );

            var dot = direction.X * toTargetNorm.X + 
                     direction.Y * toTargetNorm.Y + 
                     direction.Z * toTargetNorm.Z;

            if (dot > 0.98f && distance < closestDist)
            {
                closestDist = distance;
                closestPlayer = targetPlayer;
            }
        }

        return closestPlayer;
    }

    private CCSPlayerController? GetClosestBot(CCSPlayerController player)
    {
        var playerPawn = player.PlayerPawn.Value;
        if (playerPawn == null) return null;

        var playerPos = playerPawn.AbsOrigin;
        var bots = Utilities.GetPlayers()
            .Where(p => p.IsBot && p.IsValid && p.PawnIsAlive)
            .ToList();

        if (bots.Count == 0) return null;

        // First try looking at a bot
        var lookedAtBot = GetPlayerLookingAt(player);
        if (lookedAtBot != null && lookedAtBot.IsBot)
        {
            return lookedAtBot;
        }

        // Otherwise return closest bot (including above/below player)
        return bots
            .OrderBy(b => {
                var botPos = b.PlayerPawn.Value?.AbsOrigin;
                if (botPos == null) return float.MaxValue;
                
                // Calculate 3D distance including vertical
                var dx = playerPos.X - botPos.X;
                var dy = playerPos.Y - botPos.Y;
                var dz = playerPos.Z - botPos.Z;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            })
            .FirstOrDefault();
    }

    private void GiveFullMoney(CCSPlayerController player)
    {
        if (!player.IsValid) return;
        
        var moneyServices = player.InGameMoneyServices;
        if (moneyServices != null)
        {
            moneyServices.Account = 65535;
        }
    }
	
	private void UpdateRunBoost()
	{
		if (!_practiceMode) return;

		var players = Utilities.GetPlayers().Where(p => p.IsValid && p.PawnIsAlive && !p.IsBot).ToList();
		var bots = Utilities.GetPlayers().Where(p => p.IsValid && p.PawnIsAlive && p.IsBot).ToList();

		var currentlyOnBot = new Dictionary<ulong, string>();

		foreach (var player in players)
		{
			var playerPawn = player.PlayerPawn.Value;
			if (playerPawn == null) continue;

			var playerPos = playerPawn.AbsOrigin;
			var playerVelocity = playerPawn.AbsVelocity;
			var playerTeam = player.Team;
			var playerSteamId = player.SteamID;
			bool onBotThisTick = false;

			// Check if player is standing on any teammate bot
			foreach (var bot in bots)
			{
				// Only follow teammate bots
				if (bot.Team != playerTeam) continue;

				var botPawn = bot.PlayerPawn.Value;
				if (botPawn == null) continue;

				var botPos = botPawn.AbsOrigin;
				var botName = bot.PlayerName;
				
				// Calculate bots 2D speed
				var botVelocity = botPawn.AbsVelocity;
				var botSpeed = Math.Sqrt(botVelocity.X * botVelocity.X + botVelocity.Y * botVelocity.Y);
				
				// Check bot's current state
				bool botIsCrouched = (botPawn.Flags & (uint)PlayerFlags.FL_DUCKING) != 0;
				bool botIsWalking = botSpeed > 5.0 && botSpeed < 150.0; // Shift-walking

				// Check if player is above bot
				var horizontalDistance = Math.Sqrt(
					Math.Pow(playerPos.X - botPos.X, 2) + 
					Math.Pow(playerPos.Y - botPos.Y, 2)
				);
				var verticalDistance = playerPos.Z - botPos.Z;

				// Player is on bots head if within range
				if (horizontalDistance < 32.0f && verticalDistance > 50.0f && verticalDistance < 90.0f)
				{
					// Store bot state
					if (!_botStateWhileOn.ContainsKey(playerSteamId))
					{
						_botStateWhileOn[playerSteamId] = new BotStateInfo();
					}
					_botStateWhileOn[playerSteamId].IsCrouched = botIsCrouched;
					_botStateWhileOn[playerSteamId].IsWalking = botIsWalking;
					_botStateWhileOn[playerSteamId].Speed = (float)botSpeed;
					
					// Bot follows X/Y but keeps its own Z
					var targetPos = new Vector(playerPos.X, playerPos.Y, botPos.Z);
					var currentAngle = botPawn.AbsRotation;
					
					// Zero out bot velocity to prevent sliding
					botPawn.Teleport(targetPos, currentAngle, new Vector(0, 0, 0));
					
					// Calculate player speed for feedback
					var playerHorizontalSpeed = Math.Sqrt(
						playerVelocity.X * playerVelocity.X + 
						playerVelocity.Y * playerVelocity.Y
					);
					
					// Determine if runboost conditions are met
					bool playerIsCrouched = (playerPawn.Flags & (uint)PlayerFlags.FL_DUCKING) != 0;
					bool playerIsWalking = playerHorizontalSpeed > 5.0 && playerHorizontalSpeed < 150.0;
					
					bool runboostPossible = !botIsCrouched && !botIsWalking && 
											!playerIsCrouched && !playerIsWalking &&
											playerHorizontalSpeed >= 150.0;
					
					// Show predicted boost
					float predictedBoost = (float)playerHorizontalSpeed * 2.0f;
					string stateText = runboostPossible ? 
						$"RUNBOOST READY → {predictedBoost:F0} u/s" : 
						"Normal Jump Only";
					
					player.PrintToCenter($"{playerHorizontalSpeed:F0} u/s [{stateText}]");
					
					currentlyOnBot[playerSteamId] = botName;
					onBotThisTick = true;
					break;
				}
			}

			// Player just left bots head, apply boost if conditions met
			if (!onBotThisTick && _playerOnBotHead.ContainsKey(playerSteamId))
			{
				// Use the stored bot state from when player was on the bot
				if (_botStateWhileOn.ContainsKey(playerSteamId))
				{
					var botState = _botStateWhileOn[playerSteamId];
					bool botIsCrouched = botState.IsCrouched;
					bool botIsWalking = botState.IsWalking;
					var botSpeed = botState.Speed;
					
					// Check top players state at jump moment
					bool playerIsCrouched = (playerPawn.Flags & (uint)PlayerFlags.FL_DUCKING) != 0;
					
					// Calculate players horizontal speed at jump moment
					var horizontalSpeed = Math.Sqrt(
						playerVelocity.X * playerVelocity.X + 
						playerVelocity.Y * playerVelocity.Y
					);
					
					bool playerIsWalking = horizontalSpeed > 5.0 && horizontalSpeed < 150.0;
					
					bool runboostActive = !botIsCrouched && !botIsWalking &&
										  !playerIsCrouched && !playerIsWalking &&
										  horizontalSpeed >= 150.0;
					
					if (runboostActive)
					{
						
						float boostMultiplier = 2.0f; // DOUBLE the velocity!
						
						// Apply the runboost
						var boostedVelocity = new Vector(
							playerVelocity.X * boostMultiplier,
							playerVelocity.Y * boostMultiplier,
							268.3281f // Standard jump velocity
						);
						
						playerPawn.Teleport(playerPos, playerPawn.EyeAngles, boostedVelocity);
						
						// Calculate boosted speed
						float boostedSpeed = (float)horizontalSpeed * boostMultiplier;
						
						player.PrintToCenter($"RUNBOOST! {horizontalSpeed:F0} → {boostedSpeed:F0} u/s (x2.0)");
					}
					else
					{
						// Just apply standard jump velocity
						
						var normalVelocity = new Vector(
							playerVelocity.X,
							playerVelocity.Y,
							268.3281f // Standard jump
						);
						
						playerPawn.Teleport(playerPos, playerPawn.EyeAngles, normalVelocity);
						
						// Feedback explaining why no boost
						string reason = "";
						if (playerIsCrouched) reason = "Player Crouching";
						else if (botIsCrouched) reason = "Bot Crouching";
						else if (playerIsWalking) reason = "Player Walking";
						else if (botIsWalking) reason = "Bot Walking";
						else if (horizontalSpeed < 150.0) reason = "Speed Too Low";
						
						player.PrintToCenter($"Normal Jump - {reason} ({horizontalSpeed:F0} u/s)");
					}
					
					// Clear stored bot state
					_botStateWhileOn.Remove(playerSteamId);
				}
				
				_playerOnBotHead.Remove(playerSteamId);
			}
			else if (onBotThisTick)
			{
				_playerOnBotHead[playerSteamId] = currentlyOnBot[playerSteamId];
			}
		}
	}

    private bool IsGrenade(string weaponName)
    {
        return weaponName.Contains("grenade") || weaponName.Contains("flashbang") || 
               weaponName.Contains("molotov") || weaponName.Contains("incgrenade") ||
               weaponName.Contains("decoy");
    }

    private string GetGrenadeName(string weaponName)
    {
        if (weaponName.Contains("hegrenade")) return "HE Grenade";
        if (weaponName.Contains("flashbang")) return "Flashbang";
        if (weaponName.Contains("smokegrenade")) return "Smoke";
        if (weaponName.Contains("molotov")) return "Molotov";
        if (weaponName.Contains("incgrenade")) return "Incendiary";
        if (weaponName.Contains("decoy")) return "Decoy";
        return "Unknown";
    }

    private int GetGrenadeSlot(string weaponName)
    {
        if (weaponName.Contains("hegrenade")) return 6;
        if (weaponName.Contains("flashbang")) return 7;
        if (weaponName.Contains("smokegrenade")) return 8;
        if (weaponName.Contains("molotov") || weaponName.Contains("incgrenade")) return 10;
        if (weaponName.Contains("decoy")) return 9;
        return 3;
    }
	
	private void PlaceSpecificTeamBot(CCSPlayerController player, CsTeam team)
	{
		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn == null) return;
		
		var bots = Utilities.GetPlayers()
			.Where(p => p.IsBot && p.IsValid && p.PawnIsAlive && p.Team == team)
			.ToList();
		
		if (bots.Count == 0)
		{
			player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No {team} bots available!");
			return;
		}
		
		var steamId = player.SteamID;
		if (!_lastBotIndex.ContainsKey(steamId))
		{
			_lastBotIndex[steamId] = 0;
		}
		
		var botIndex = _lastBotIndex[steamId] % bots.Count;
		var targetBot = bots[botIndex];
		_lastBotIndex[steamId] = (botIndex + 1) % bots.Count;
		
		if (targetBot != null && targetBot.IsBot && targetBot.PawnIsAlive)
		{
			var pos = playerPawn.AbsOrigin;
			var eyeAngles = playerPawn.EyeAngles;
			var angle = new QAngle(0, eyeAngles.Y, 0);
			
			var botPawn = targetBot.PlayerPawn.Value;
			if (botPawn != null)
			{
				var isEnemy = (player.Team == CsTeam.CounterTerrorist && targetBot.Team == CsTeam.Terrorist) ||
							  (player.Team == CsTeam.Terrorist && targetBot.Team == CsTeam.CounterTerrorist);
				
				// Only give noclip if placing ENEMY bot
				if (isEnemy)
				{
					playerPawn.MoveType = MoveType_t.MOVETYPE_NOCLIP;
					Schema.SetSchemaValue(playerPawn.Handle, "CBaseEntity", "m_nActualMoveType", 8);
				}
				
				// Teleport bot
				AddTimer(0.1f, () =>
				{
					if (targetBot.IsValid && targetBot.PawnIsAlive && targetBot.PlayerPawn.Value != null)
					{
						var pawn = targetBot.PlayerPawn.Value;
						pawn.Teleport(pos, angle, new Vector(0, 0, 0));
						
						// Save respawn position
						var botName = targetBot.PlayerName;
						_botRespawnPosition[botName] = new Vector(pos.X, pos.Y, pos.Z);
						_botRespawnAngle[botName] = new QAngle(0, eyeAngles.Y, 0);
					}
				});
				
				// Remove noclip after 3 seconds (only if enemy bot)
				if (isEnemy)
				{
					AddTimer(3.0f, () =>
					{
						if (player.IsValid && player.PawnIsAlive && playerPawn.IsValid)
						{
							playerPawn.MoveType = MoveType_t.MOVETYPE_WALK;
							Schema.SetSchemaValue(playerPawn.Handle, "CBaseEntity", "m_nActualMoveType", 2);
						}
					});
					
					player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed enemy bot #{botIndex + 1}! (3s noclip)");
				}
				else
				{
					player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} Placed teammate bot #{botIndex + 1}!");
				}
			}
		}
	}
	
    private void TeleportPlayer(CCSPlayerController player, Vector pos, QAngle angle)
	{
		var pawn = player.PlayerPawn.Value;
		if (pawn == null) return;

		// Teleport with jump velocity
		pawn.Teleport(pos, angle, new Vector(0, 0, 300.0f));  // 300 = jump force
		
		// Set eye angles
		AddTimer(0.05f, () =>
		{
			if (player.IsValid && player.PawnIsAlive && player.PlayerPawn.Value != null)
			{
				var p = player.PlayerPawn.Value;
				p.EyeAngles.X = angle.X;
				p.EyeAngles.Y = angle.Y;
				p.EyeAngles.Z = angle.Z;
			}
		});
	}

    private void BrowseSpawns(CCSPlayerController player, CsTeam team, int? targetIndex = null)
    {
        var steamId = player.SteamID;
        var spawns = Utilities.FindAllEntitiesByDesignerName<SpawnPoint>(
            team == CsTeam.Terrorist ? "info_player_terrorist" : "info_player_counterterrorist"
        ).ToList();

        if (spawns.Count == 0)
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} No spawn points found!");
            return;
        }

        var spawnDict = team == CsTeam.Terrorist ? _selectedTSpawn : _selectedCTSpawn;
        
        if (!spawnDict.ContainsKey(steamId))
        {
            spawnDict[steamId] = 0;
        }

        int currentIndex;
        if (targetIndex.HasValue)
        {
            if (targetIndex.Value < 0 || targetIndex.Value >= spawns.Count)
            {
                player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Invalid spawn! Use 1-{spawns.Count}");
                return;
            }
            currentIndex = targetIndex.Value;
            spawnDict[steamId] = (currentIndex + 1) % spawns.Count;
        }
        else
        {
            currentIndex = spawnDict[steamId];
            spawnDict[steamId] = (currentIndex + 1) % spawns.Count;
        }

        var spawn = spawns[currentIndex];
        
        if (spawn?.AbsOrigin == null || spawn?.AbsRotation == null)
        {
            player.PrintToChat($" {ChatColors.Red}[Practice]{ChatColors.Default} Invalid spawn point!");
            return;
        }

        TeleportPlayer(player, spawn.AbsOrigin, spawn.AbsRotation);

        var teamName = team == CsTeam.Terrorist ? "T" : "CT";
        player.PrintToChat($" {ChatColors.Green}[Practice]{ChatColors.Default} {teamName} Spawn {currentIndex + 1}/{spawns.Count}");
        
        if (!targetIndex.HasValue)
        {
            player.PrintToChat($" {ChatColors.Grey}Use !{(team == CsTeam.Terrorist ? "tspawn" : "ctspawn")} again to cycle or add a number (e.g., !{(team == CsTeam.Terrorist ? "tspawn" : "ctspawn")} 5)");
        }
    }

    private void SaveNadesToFile(ulong steamId)
    {
        if (!_savedNades.ContainsKey(steamId)) return;

        try
        {
            var filePath = Path.Combine(_pluginPath, $"nades_{steamId}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(_savedNades[steamId], new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            Console.WriteLine($"[PracticeMode] Saved {_savedNades[steamId].Count} nades for {steamId} to {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PracticeMode] Error saving nades: {ex.Message}");
        }
    }

    private void LoadNadesFromFile(ulong steamId)
    {
        try
        {
            var filePath = Path.Combine(_pluginPath, $"nades_{steamId}.json");
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var nades = System.Text.Json.JsonSerializer.Deserialize<List<GrenadePosition>>(json);
                if (nades != null)
                {
                    _savedNades[steamId] = nades;
                    Console.WriteLine($"[PracticeMode] Loaded {nades.Count} nades for {steamId} from {filePath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PracticeMode] Error loading nades: {ex.Message}");
        }
    }
}
