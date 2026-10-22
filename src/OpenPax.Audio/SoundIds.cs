namespace OpenPax.Audio;

/// <summary>
/// Which file each sound id plays (the compare tree; paths under
/// the game's Sound folder, matched without regard to case). Ids below 33000
/// are effects, the rest voices and ambience loops.
/// </summary>
public static class SoundIds
{
    public const int FirstVoice = 33000;

    /// <summary>A widget plays its id on press and its id plus this on release.</summary>
    public const int Release = 100;

    public const int SpinnerUp = 3000;
    public const int BackArrow = 12000;
    public const int TopBarPress = 15000;
    public const int HistoryToggle = 15100;
    public const int Explosion = 28600;
    public const int BattleWindowOpens = 33400;
    public const int BattleStations = 33500;
    public const int GameSaved = 34900;
    public const int ResearchComplete = 36500;
    public const int ShipComplete = 36700;
    public const int SimulationComplete = 36800;
    public const int SpeciesComplete = 36900;
    public const int SpeciesDeclined = 37000;
    public const int SpeciesSelected = 37100;
    public const int What = 37700;
    public const int YesCommander = 37800;
    public const int PlanetDefeated = 37900;
    public const int TreatyOffered = 38000;
    public const int SpyFailed = 38100;
    public const int SpySucceeded = 38200;
    public const int SpyCaught = 38300;
    public const int TradePending = 38400;
    public const int TradeDeclined = 38500;
    public const int TradeAccepted = 38600;
    public const int SpeciesAmbience = 45000;
    public const int BridgeAmbience = 45100;

    /// <summary>The voices that answer an order the game takes on the map.</summary>
    public static readonly int[] MapAccepted = [33000, 34300, 34200, 33300, 34000, 37800];

    /// <summary>The voices that answer an order the game refuses on the map.</summary>
    public static readonly int[] MapRefused = [35400, 35300, 34800, 34100, 35200];

    /// <summary>The voices that answer a battle order.</summary>
    public static readonly int[] BattleAccepted = [33000, 33300, 36200, 37800];

    private static readonly Dictionary<int, string> _files = Build();

    /// <summary>The file of an id, relative to the Sound folder with '/' separators, or null for an id with none.</summary>
    public static string? File(int id) => _files.GetValueOrDefault(id);

    public static IEnumerable<int> All => _files.Keys;

    public static bool IsVoice(int id) => id >= FirstVoice;

    private static Dictionary<int, string> Build()
    {
        var files = new Dictionary<int, string>
        {
            [3000] = "Buttons/Clicks/Tickerup.wav",
            [3100] = "Buttons/Clicks/Tickerdn.wav",
            [7000] = "Buttons/Clicks/Arrowin1.wav",
            [7100] = "Buttons/Clicks/Arrowout.wav",
            [8000] = "Buttons/Tones/Tone1in.wav",
            [8100] = "Buttons/Tones/Tone1out.wav",
            [11000] = "Buttons/Clicks/Btnin.wav",
            [11100] = "Buttons/Clicks/Btnout.wav",
            [12000] = "Misc/Chngwin.wav",
            [13000] = "Buttons/Tones/Tone1in.wav",
            [13100] = "Buttons/Tones/Tone1out.wav",
            [14000] = "Buttons/Tones/Tone2in.wav",
            [14100] = "Buttons/Tones/Tone2out.wav",
            [15000] = "Buttons/Clicks/Btnin.wav",
            [15100] = "Misc/Slide2.wav",
            [15200] = "Buttons/Clicks/Btnin.wav",
            [15300] = "Misc/Slideout.wav",
            [15400] = "Buttons/Tones/Confirm1.wav",
            [15500] = "Buttons/Tones/Confirm2.wav",
            [15600] = "Buttons/Tones/Noselect.wav",
            [15700] = "Buttons/Tones/Softpres.wav",
            [15800] = "Buttons/Tones/Tone3.wav",
            [15900] = "Buttons/Tones/Tone4.wav",
            [16000] = "Buttons/Clicks/Switch.wav",
            [16100] = "Misc/Youlose.wav",
            [16200] = "Alerts/Alert_1.wav",
            [16300] = "Alerts/Alert_2.wav",
            [16400] = "Alerts/Alert_3.wav",
            [16500] = "Temp/Surfloop.wav",
            [16600] = "Temp/Wormhole.wav",
            [17000] = "Misc/Slideout.wav",
            [20100] = "Species/Selects/Frntlock.wav",
            [22000] = "Species/Buttons/Aceptin.wav",
            [22100] = "Species/Buttons/Aceptout.wav",
            [22200] = "Species/Buttons/Dsgnin.wav",
            [22300] = "Species/Buttons/Dsgnout.wav",
            [22400] = "Species/Buttons/Arrowbtn.wav",
            [26000] = "Buttons/Tones/Addbtnhi.wav",
            [26100] = "Buttons/Tones/Addbtnlo.wav",
            [26001] = "Buttons/Tones/Remvin.wav",
            [26101] = "Buttons/Tones/Remvout.wav",
            [28600] = "Explosio/Surface/Xplosn1.wav",
            [28800] = "Explosio/Surface/Xplosn2.wav",
            [30000] = "Explosio/Surface/Xplosn3.wav",
            [30200] = "Explosio/Surface/Xplosn4.wav",
            [30300] = "Explosio/Warped/Warped1.wav",
            [30400] = "Explosio/Warped/Warped2.wav",
            [30500] = "Explosio/Warped/Warped2.wav",
            [33000] = "Voices/Accepted.WAV",
            [33300] = "Voices/Affirmative.WAV",
            [33400] = "Voices/AwaitingOrders.WAV",
            [33500] = "Voices/BattleStations.WAV",
            [34000] = "Voices/CoordReceived.WAV",
            [34100] = "Voices/Declined.WAV",
            [34200] = "Voices/EnginesEngagedCmdr.WAV",
            [34300] = "Voices/EnginesEngaged.WAV",
            [34800] = "Voices/Failed.WAV",
            [34900] = "Voices/GameSaved.WAV",
            [35000] = "Voices/IncomingTrans.WAV",
            [35200] = "Voices/Negative.WAV",
            [35300] = "Voices/NoCmdr.WAV",
            [35400] = "Voices/No.WAV",
            [36200] = "Voices/ProcessingReq.WAV",
            [36500] = "Voices/ResearchComplete.WAV",
            [36700] = "Voices/ShipComplete.WAV",
            [36800] = "Voices/SimulationComplete.WAV",
            [36900] = "Voices/SpeciesComplete.WAV",
            [37000] = "Voices/SpeciesDeclined.WAV",
            [37100] = "Voices/SpeciesSelected.WAV",
            [37700] = "Voices/What.WAV",
            [37800] = "Voices/YesCommander.WAV",
            [37900] = "Voices/PlanetDefeated02.WAV",
            [38000] = "Voices/TreatyOffered03.WAV",
            [38100] = "Voices/EspianageFailed01.WAV",
            [38200] = "Voices/EspionageSuccessful02.WAV",
            [38300] = "Voices/EspianageIntercept02.WAV",
            [38400] = "Voices/TradePending01.WAV",
            [38500] = "Voices/TradeDeclined01.WAV",
            [38600] = "Voices/TradeAccepted.WAV",
            [45000] = "Species/Ambience/Specamb.wav",
            [45100] = "Cntrlrm/Crloop1.wav",
            [45200] = "Cntrlrm/Crloop2.wav",
            [45300] = "Cntrlrm/Hololoop.wav",
        };

        // Species figures by position: far left, left, middle, right, far right.
        string[] selects = ["Flselect", "Lselect", "Mselect", "Rselect", "Frselect"];
        string[] returns = ["Flrtnnlk", "Lrtnnlk", "Mrtnnlk", "Rrtnnlk", "Frrtnnlk"];
        string[] locks = ["Fllock", "Llock", "Mlock", "Rlock", "Frlock"];
        for (var k = 0; k < 5; k++)
        {
            files[20000 + k] = $"Species/Selects/{selects[k]}.wav";
            files[20200 + k] = $"Species/Returns/{returns[k]}.wav";
            files[20300 + k] = $"Species/Returns/Backlock/{locks[k]}.wav";
        }

        // Ship explosions; there is no Exp6.
        int[] ships = [1, 2, 3, 4, 5, 7, 8, 9];
        for (var i = 0; i < ships.Length; i++)
        {
            files[27000 + (i * 200)] = $"Explosio/Ships/Exp{ships[i]}.wav";
        }

        for (var i = 0; i < 7; i++)
        {
            files[31000 + (i * 10)] = $"Guns/Laser{i + 1}.wav";
        }

        for (var i = 0; i < 5; i++)
        {
            files[31200 + (i * 10)] = $"Guns/Missle{i + 1}.wav";
        }

        for (var i = 0; i < 6; i++)
        {
            files[31300 + (i * 10)] = $"Guns/Shell{i + 1}.wav";
        }

        return files;
    }
}
