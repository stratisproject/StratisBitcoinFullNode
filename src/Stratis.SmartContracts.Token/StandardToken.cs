using Stratis.SmartContracts;
using Stratis.SmartContracts.Token;

/// <summary>
/// Implementation of a standard token contract for the Stratis Platform.
/// DISCLAIMER: For demonstration purposes only.
/// </summary>
public class StandardToken : SmartContract, IStandardToken
{
    /// <summary>
    /// Constructor used to create a new instance of the token. Assigns the total token supply to the creator of the contract.
    /// </summary>
    /// <param name="smartContractState">The execution state for the contract.</param>
    /// <param name="totalSupply">The total token supply.</param>
    public StandardToken(ISmartContractState smartContractState, uint totalSupply) 
        : base(smartContractState)
    {
        this.TotalSupply = totalSupply;
        this.SetBalance(Message.Sender, totalSupply);
    }

    /// <inheritdoc />
    public uint TotalSupply
    {
        get => PersistentState.GetUInt32(nameof(this.TotalSupply));
        private set => PersistentState.SetUInt32(nameof(this.TotalSupply), value);
    }

    /// <inheritdoc />
    public uint GetBalance(Address address)
    {
        return PersistentState.GetUInt32($"Balance:{address}");
    }

    private void SetBalance(Address address, uint value)
    {
        PersistentState.SetUInt32($"Balance:{address}", value);
    }

    /// <inheritdoc />
    public bool Transfer(Address to, uint amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = Message.Sender, To = to, Amount = 0 });

            return true;
        }

        uint senderBalance = GetBalance(Message.Sender);
        
        if (senderBalance < amount)
        {
            return false;
        }

        uint toBalance = GetBalance(to);

        SetBalance(Message.Sender, senderBalance - amount);

        SetBalance(to, checked(toBalance + amount));

        Log(new TransferLog { From = Message.Sender, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool TransferFrom(Address from, Address to, uint amount)
    {
        if (amount == 0)
        {
            Log(new TransferLog { From = from, To = to, Amount = 0 });

            return true;
        }

        uint senderAllowance = Allowance(from, Message.Sender);
        uint fromBalance = GetBalance(from);

        if (senderAllowance < amount || fromBalance < amount)
        {
            return false;
        }

        uint toBalance = GetBalance(to);

        SetApproval(from, Message.Sender, senderAllowance - amount);
        SetBalance(from, fromBalance - amount);

        SetBalance(to, checked(toBalance + amount));

        Log(new TransferLog { From = from, To = to, Amount = amount });

        return true;
    }

    /// <inheritdoc />
    public bool Approve(Address spender, uint amount)
    {
        SetApproval(Message.Sender, spender, amount);

        Log(new ApprovalLog { Owner = Message.Sender, Spender = spender, Amount = amount});

        return true;
    }

    private void SetApproval(Address owner, Address spender, uint value)
    {
        PersistentState.SetUInt32($"Allowance:{owner}:{spender}", value);
    }

    /// <inheritdoc />
    public uint Allowance(Address owner, Address spender)
    {
        return PersistentState.GetUInt32($"Allowance:{owner}:{spender}");
    }

    public struct TransferLog
    {
        [Index]
        public Address From;

        [Index]
        public Address To;

        public uint Amount;
    }

    public struct ApprovalLog
    {
        [Index]
        public Address Owner;

        [Index]
        public Address Spender;

        public uint Amount;
    }
}