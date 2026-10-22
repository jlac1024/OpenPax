using System.Security.Cryptography;

namespace OpenPax.Net.Lobby;

/// <summary>
/// Carries a game's traffic between its host and each member through the
/// lobby, so nobody opens a port. A ticket's two ends each connect with
/// it; once both are in, the bytes are copied both ways, unread, until
/// either end closes. A ticket not used within a minute lapses.
/// </summary>
internal sealed class Relay
{
    public const int TicketLife = 60_000;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (Stream? Host, Stream? Member, long Issued)> _tickets = [];

    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

    public void Issue(string token, long now)
    {
        lock (_lock)
        {
            _tickets[token] = (null, null, now);
        }
    }

    /// <summary>One end arrives; false when the ticket is unknown or that end is already in.</summary>
    public bool Arrive(string token, bool host, Stream stream)
    {
        (Stream? Host, Stream? Member, long Issued) ticket;
        lock (_lock)
        {
            if (!_tickets.TryGetValue(token, out ticket) || (host ? ticket.Host : ticket.Member) is not null)
            {
                return false;
            }

            ticket = host ? ticket with { Host = stream } : ticket with { Member = stream };
            if (ticket.Host is null || ticket.Member is null)
            {
                _tickets[token] = ticket;
                return true;
            }

            _tickets.Remove(token);
        }

        Pipe(ticket.Host, ticket.Member);
        return true;
    }

    public void Expire(long now)
    {
        lock (_lock)
        {
            foreach (var (token, ticket) in _tickets.Where(t => now - t.Value.Issued > TicketLife).ToList())
            {
                ticket.Host?.Dispose();
                ticket.Member?.Dispose();
                _tickets.Remove(token);
            }
        }
    }

    private static void Pipe(Stream a, Stream b)
    {
        void Copy(Stream from, Stream to)
        {
            try
            {
                from.CopyTo(to);
            }
            catch (Exception e) when (e is IOException or ObjectDisposedException)
            {
                // One end went; the other is closed below.
            }
            finally
            {
                a.Dispose();
                b.Dispose();
            }
        }

        new Thread(() => Copy(a, b)) { IsBackground = true, Name = "OpenPax relay" }.Start();
        new Thread(() => Copy(b, a)) { IsBackground = true, Name = "OpenPax relay" }.Start();
    }
}
