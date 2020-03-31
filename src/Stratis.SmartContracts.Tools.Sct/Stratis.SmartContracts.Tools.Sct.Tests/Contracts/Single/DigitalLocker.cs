using Stratis.SmartContracts;

public class DigitalLocker : SmartContract
{
    public const string Pending = "Pending";
    public const string Rejected = "Rejected";
    public const string Approved = "Approved";
    public const string Shared = "Shared";
    public const string Available = "Available";

    public DigitalLocker(ISmartContractState state, string lockerFriendlyName, Address bankAgent)
        : base(state)
    {
        Owner = Message.Sender;
        LockerFriendlyName = lockerFriendlyName;
        BankAgent = bankAgent;
        State = (uint)StateType.Requested;
    }

    public enum StateType : uint
    {
        Requested = 0,
        DocumentReview = 1,
        AvailableToShare = 2,
        SharingRequestPending = 3,
        SharingWithThirdParty = 4,
        Terminated = 5
    }

    public Address Owner
    {
        get => PersistentState.GetAddress(nameof(Owner));
        private set => PersistentState.SetAddress(nameof(Owner), value);
    }

    public Address BankAgent
    {
        get => PersistentState.GetAddress(nameof(BankAgent));
        private set => PersistentState.SetAddress(nameof(BankAgent), value);
    }

    public string LockerFriendlyName
    {
        get => PersistentState.GetString(nameof(LockerFriendlyName));
        private set => PersistentState.SetString(nameof(LockerFriendlyName), value);
    }

    public string LockerIdentifier
    {
        get => PersistentState.GetString(nameof(LockerIdentifier));
        private set => PersistentState.SetString(nameof(LockerIdentifier), value);
    }

    public Address CurrentAuthorizedUser
    {
        get => PersistentState.GetAddress(nameof(CurrentAuthorizedUser));
        private set => PersistentState.SetAddress(nameof(CurrentAuthorizedUser), value);
    }

    public string ExpirationDate
    {
        get => PersistentState.GetString(nameof(ExpirationDate));
        private set => PersistentState.SetString(nameof(ExpirationDate), value);
    }

    public string Image
    {
        get => PersistentState.GetString(nameof(Image));
        private set => PersistentState.SetString(nameof(Image), value);
    }

    public Address ThirdPartyRequestor
    {
        get => PersistentState.GetAddress(nameof(ThirdPartyRequestor));
        private set => PersistentState.SetAddress(nameof(ThirdPartyRequestor), value);
    }

    public string IntendedPurpose
    {
        get => PersistentState.GetString(nameof(IntendedPurpose));
        private set => PersistentState.SetString(nameof(IntendedPurpose), value);
    }

    public string LockerStatus
    {
        get => PersistentState.GetString(nameof(LockerStatus));
        private set => PersistentState.SetString(nameof(LockerStatus), value);
    }

    public string RejectionReason
    {
        get => PersistentState.GetString(nameof(RejectionReason));
        private set => PersistentState.SetString(nameof(RejectionReason), value);
    }

    public uint State
    {
        get => PersistentState.GetUInt32(nameof(State));
        private set => PersistentState.SetUInt32(nameof(State), value);
    }

    public void BeginReviewProcess()
    {
        Assert(Message.Sender != Owner);
        Assert(State == (uint)StateType.Requested);

        BankAgent = Message.Sender;
        LockerStatus = Pending;
        State = (uint)StateType.DocumentReview;
    }

    public void RejectApplication(string rejectionReason)
    {
        Assert(Message.Sender == BankAgent);

        RejectionReason = rejectionReason;
        LockerStatus = Rejected;
        State = (uint)StateType.DocumentReview;
    }

    public void UploadDocuments(string lockerIdentifier, string image)
    {
        Assert(Message.Sender == BankAgent);

        LockerStatus = Approved;
        Image = image;
        LockerIdentifier = lockerIdentifier;
        State = (uint)StateType.AvailableToShare;
    }

    public void ShareWithThirdParty(Address thirdPartyRequestor, string expirationDate, string intendedPurpose)
    {
        Assert(Message.Sender == Owner);

        ThirdPartyRequestor = thirdPartyRequestor;
        CurrentAuthorizedUser = thirdPartyRequestor;
        LockerStatus = Shared;
        IntendedPurpose = intendedPurpose;
        ExpirationDate = expirationDate;
        State = (uint)StateType.SharingWithThirdParty;
    }

    public void AcceptSharingRequest()
    {
        Assert(Message.Sender == Owner);

        CurrentAuthorizedUser = ThirdPartyRequestor;
        State = (uint)StateType.SharingWithThirdParty;
    }

    public void RejectSharingRequest()
    {
        Assert(Message.Sender == Owner);

        LockerStatus = Available;
        CurrentAuthorizedUser = Address.Zero;
        State = (uint)StateType.AvailableToShare;
    }

    public void RequestLockerAccess(string intendedPurpose)
    {
        Assert(Message.Sender != Owner);

        ThirdPartyRequestor = Message.Sender;
        IntendedPurpose = intendedPurpose;
        State = (uint)StateType.SharingRequestPending;
    }

    public void ReleaseLockerAccess()
    {
        Assert(Message.Sender == CurrentAuthorizedUser);

        LockerStatus = Available;
        ThirdPartyRequestor = Address.Zero;
        CurrentAuthorizedUser = Address.Zero;
        IntendedPurpose = string.Empty;
        State = (uint)StateType.AvailableToShare;
    }

    public void RevokeAccessFromThirdParty()
    {
        Assert(Message.Sender == Owner);

        LockerStatus = Available;
        CurrentAuthorizedUser = Address.Zero;
        State = (uint)StateType.AvailableToShare;
    }

    public void Terminate()
    {
        Assert(Message.Sender == Owner);

        CurrentAuthorizedUser = Address.Zero;
        State = (uint)StateType.Terminated;
    }
}