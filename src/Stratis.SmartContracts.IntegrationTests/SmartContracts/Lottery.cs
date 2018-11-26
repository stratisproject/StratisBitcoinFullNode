using System;
using Stratis.SmartContracts;

/// <summary>
/// A once-off lottery that uses secret reveals to generate randomness.
/// </summary>
public class Lottery : SmartContract
{
    public const int DesiredPlayers = 2;

    public uint PlayerCount
    {
        get
        {
            return PersistentState.GetUInt32(nameof(PlayerCount));
        }
        private set
        {
            PersistentState.SetUInt32(nameof(PlayerCount), value);
        }
    }

    public uint WinningNumber
    {
        get
        {
            return PersistentState.GetUInt32(nameof(WinningNumber));
        }
        private set
        {
            PersistentState.SetUInt32(nameof(WinningNumber), value);
        }
    }

    public bool Decided
    {
        get
        {
            return PersistentState.GetBool(nameof(Decided));
        }
        private set
        {
            PersistentState.SetBool(nameof(Decided), value);
        }
    }

    private void AddPlayer(Address address)
    {
        PersistentState.SetAddress($"Player{PlayerCount}", address);
        PersistentState.SetBool($"Added:{address}", true);
        PlayerCount++;
    }

    private bool IsPlayerAdded(Address address)
    {
        return PersistentState.GetBool($"Added:{address}");
    }

    private Address GetPlayer(uint num)
    {
        return PersistentState.GetAddress($"Player{num}");
    }

    public Lottery(ISmartContractState state) : base(state)
    {
    }

    public void Join()
    {
        Assert(Message.Value == 100_000_000); // 1 STRAT entry
        Assert(!IsPlayerAdded(Message.Sender));
        Assert(!Decided);
        Assert(PlayerCount < DesiredPlayers);
        AddPlayer(Message.Sender);
    }

    /// <summary>
    /// Selects a winner based on the addresses involved. Really not random but unlikely to be gamed at this level.
    /// </summary>
    public Address SelectWinner()
    {
        Assert(!Decided);
        Assert(PlayerCount == DesiredPlayers);

        byte[] toHash = new byte[PlayerCount * 32];
        for (uint i = 0; i < PlayerCount; i++)
        {
            byte[] playerBytes = GetPlayer(i).ToBytes();
            Array.Copy(playerBytes, 0, toHash, i * 32, playerBytes.Length);
        }

        byte[] hashed = Keccak256(toHash);
        uint winner = Serializer.ToUInt32(hashed) % PlayerCount;
        WinningNumber = winner;
        Decided = true;
        return GetPlayer(winner);
    }

    public void Claim()
    {
        Assert(Decided);
        Assert(Balance > 0); // Hasn't paid out
        Assert(Message.Sender == GetPlayer(WinningNumber));
        Transfer(Message.Sender, Balance);
    }
}