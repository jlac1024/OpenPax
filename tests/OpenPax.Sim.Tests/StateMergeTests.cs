using OpenPax.Sim.Saving;
using OpenPax.Sim.Setup;

namespace OpenPax.Sim.Tests;

public class StateMergeTests
{
    private static byte[] Bytes(Game game)
    {
        using var stream = new MemoryStream();
        SaveFile.Write(stream, game, game.Tables);
        return stream.ToArray();
    }

    private static Game Copy(Game game)
    {
        using var stream = new MemoryStream(Bytes(game));
        return Assert.IsType<Game>(SaveFile.Read(stream, game.Tables, null));
    }

    [Fact]
    public void ANewerStateMergedInHashesAsTheNewerAndKeepsWhatScreensHold()
    {
        var tables = GameTablesForTests.Load();
        var server = NewGame.Create(new GameSetup(2024, 30, 4, 3, [new HumanPlayerSetup("Commander", 0, 0, tables.Species[0])]), tables);
        for (var i = 0; i < 500; i++)
        {
            server.Step();
        }

        var client = Copy(server);
        var fleet = client.Players[0].Fleets[0];
        var star = client.Universe.Stars[3];
        var player = client.Players[1];

        for (var i = 0; i < 1500; i++)
        {
            server.Step();
        }

        StateMerge.Into(client, Copy(server));
        Assert.Equal(server.Hash(), client.Hash());
        Assert.Equal(Bytes(server), Bytes(client));
        Assert.Equal(server.Tick, client.Tick);
        Assert.Same(star, client.Universe.Stars[3]);
        Assert.Same(player, client.Players[1]);
        if (client.Players[0].Fleets.Any(f => f.ObjectId == fleet.ObjectId))
        {
            Assert.Contains(fleet, client.Players[0].Fleets);
        }
    }
}
