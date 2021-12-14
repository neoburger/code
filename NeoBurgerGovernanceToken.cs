using System;
using System.Numerics;
using Neo;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;
using Neo.SmartContract;

namespace NeoBurger
{
    [ManifestExtra("Author", "NEOBURGER")]
    [ManifestExtra("Email", "developer@neo.org")]
    [ManifestExtra("Description", "NeoBurger Governance Token")]
    [SupportedStandards("NEP-17")]
    [ContractPermission("*", "*")]
    public class NeoBurgerGovernanceToken : Nep17Token
    {
        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private const UInt160 INITIAL_HOLDER = default;
        private const ulong VOTING_PERIOD = 86400000 * 7;
        private const byte PREFIX_PROPOSAL = 0x03;
        private const byte PREFIX_PROPOSAL_EXECUTED_TIME = 0x05;
        private const byte PREFIX_DELEGATE = 0x81;
        private const byte PREFIX_DEFAULT_DELEGATE = 0x82;
        private const byte PREFIX_DELEGATE_THRESHOLD = 0x83;
        private const byte PREFIX_VOTE = 0xc1;

        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";
        public static BigInteger GetVotingPeriod() => VOTING_PERIOD;

        public static object[] ProposalAttributes(BigInteger id)
        {
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = (ProposalAttributesStruct)proposal_id_map.GetObject((ByteString)id);
            byte[] proposal_executed_time_bytearray = new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME };
            StorageMap proposal_executed_time_map = new(Storage.CurrentContext, proposal_executed_time_bytearray);
            BigInteger executed_time = (BigInteger)proposal_executed_time_map.Get((ByteString)id);
            return new object[] { proposal_attributes.scripthash, proposal_attributes.method, proposal_attributes.args, proposal_attributes.voting_deadline, executed_time };
        }

        public static UInt160 GetDelegate(UInt160 from) => (UInt160)new StorageMap(Storage.CurrentContext, PREFIX_DELEGATE).Get(from);
        public static UInt160 GetDefaultDelegate() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_DEFAULT_DELEGATE });
        public static BigInteger GetDefaultDelegateBalance() => BalanceOf(GetDefaultDelegate());
        public static BigInteger GetDelegateThreshold() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_DELEGATE_THRESHOLD });
        public static bool IsValidDelegate(UInt160 account) => account is not null && account.IsValid && (BalanceOf(account) > GetDelegateThreshold());
        public static BigInteger GetVote(UInt160 from, BigInteger proposal_index) => (BigInteger)new StorageMap(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_index).Get(from);
        public static Iterator GetVotersOfProposal(BigInteger proposal_id) => new StorageMap(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_id).Find((FindOptions)((byte)FindOptions.KeysOnly + (byte)FindOptions.RemovePrefix));

        struct ProposalAttributesStruct
        {
            public BigInteger id;
            public UInt160 scripthash;
            public string method;
            public ByteString[] args;
            public BigInteger voting_deadline;
        }

        public static void _deploy(object data, bool update)
        {
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = new();
            proposal_attributes.id = 0;
            proposal_id_map.PutObject((ByteString)(BigInteger)0, proposal_attributes);
            Mint(INITIAL_HOLDER, 10_000_000_000_000_000);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_DEFAULT_DELEGATE }, INITIAL_HOLDER);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_DELEGATE_THRESHOLD }, 100_000_000_000_000);
        }
        public static void BecomeDefaultDelegate(UInt160 account)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            byte[] default_delegate_bytearray = new byte[] { PREFIX_DEFAULT_DELEGATE };
            UInt160 default_delegate = (UInt160)Storage.Get(Storage.CurrentContext, default_delegate_bytearray);
            BigInteger to_account_balance = BalanceOf(account);
            BigInteger default_delegate_balance = BalanceOf(default_delegate);
            if (to_account_balance > default_delegate_balance && to_account_balance > GetDelegateThreshold())
                Storage.Put(Storage.CurrentContext, default_delegate_bytearray, account);
            else
                throw new Exception("No enough tokens. You need "+(ByteString)default_delegate_balance+" NOBUGs to be the default delegate");
        }

        public static BigInteger NewProposal(BigInteger proposal_id, UInt160 scripthash, string method, ByteString[] args)
        {
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            if ((BigInteger)proposal_id_map.Get((ByteString)proposal_id) != 0 || ((ProposalAttributesStruct)proposal_id_map.GetObject((ByteString)(proposal_id - 1))).id != proposal_id - 1)
                throw new Exception("Invalid proposal id");
            ProposalAttributesStruct proposal_attributes = new();
            proposal_attributes.id = proposal_id;
            proposal_attributes.scripthash = scripthash;
            proposal_attributes.method = method;
            proposal_attributes.args = args;
            proposal_attributes.voting_deadline = Runtime.Time + VOTING_PERIOD;
            proposal_id_map.PutObject((ByteString)proposal_id, proposal_attributes);
            return proposal_id;
        }

        public static void Delegate(UInt160 from, UInt160 to)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(from));
            StorageMap delegate_map = new(Storage.CurrentContext, PREFIX_DELEGATE);
            if (to == UInt160.Zero || to == from)
                delegate_map.Delete(from);
            else
                delegate_map.Put(from, to);
        }

        public static void Vote(UInt160 from, BigInteger proposal_index, bool for_or_against)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(from));
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = (ProposalAttributesStruct)proposal_id_map.GetObject((ByteString)proposal_index);
            BigInteger voting_deadline = proposal_attributes.voting_deadline;
            if(Runtime.Time > voting_deadline)
                throw new Exception("Cannot vote after the deadline");
            StorageMap vote_map = new(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_index);
            if (for_or_against)
                vote_map.Put(from, 1);
            else
                vote_map.Delete(from);
        }

        public static BigInteger CountVote(BigInteger proposal_id)
        {
            BigInteger sum_votes = 0;
            Iterator voters = new StorageMap(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_id)
                .Find((FindOptions)((byte)FindOptions.KeysOnly + (byte)FindOptions.RemovePrefix));
            bool default_delegate_voted = GetVote(GetDefaultDelegate(), proposal_id) > 0;
            while (voters.Next())
            {
                UInt160 current_voter = (UInt160)(byte[])voters.Value;
                if (GetVote(current_voter, proposal_id) > 0)
                    sum_votes += BalanceOf(current_voter);
                else {
                    UInt160 current_delegate = GetDelegate(current_voter);
                    if (IsValidDelegate(current_delegate))
                    {
                        if (GetVote(current_delegate, proposal_id) > 0)
                            sum_votes += BalanceOf(current_voter);
                    }
                    else
                    {
                        if (default_delegate_voted)
                            sum_votes += BalanceOf(current_voter);
                    }
                }
            }
            return sum_votes;
        }

        public static object ExecuteProposal(BigInteger proposal_id, UInt160[] voters)
        {
            object[] attributes = ProposalAttributes(proposal_id);
            UInt160 scripthash = (UInt160)attributes[0];
            string method = (string)attributes[1];
            ByteString[] args = (ByteString[])attributes[2];
            BigInteger voting_deadline = (BigInteger)attributes[3];
            BigInteger proposal_executed = (BigInteger)attributes[4];
            if (Runtime.Time > voting_deadline)
                throw new Exception("Cannot execute proposal after the deadline");
            if (proposal_executed > 0)
                throw new Exception("Proposal already executed");

            BigInteger voter_count = voters.Length;
            BigInteger sum_votes = 0;
            if (voter_count == 0)
                sum_votes = CountVote(proposal_id);
            else
            {
                bool default_delegate_voted = GetVote(GetDefaultDelegate(), proposal_id) > 0;
                for (BigInteger i = 0; i < voter_count; i++)
                {
                    UInt160 current_voter = voters[(uint)i];
                    //if (current_voter is null || !current_voter.IsValid)
                    //    throw new Exception(current_voter);
                    if (GetVote(current_voter, proposal_id) > 0)
                        sum_votes += BalanceOf(current_voter);
                    else
                    {
                        UInt160 current_delegate = GetDelegate(current_voter);
                        if (IsValidDelegate(current_delegate))
                        {
                            if (GetVote(current_delegate, proposal_id) > 0)
                                sum_votes += BalanceOf(current_voter);
                        }
                        else
                        {
                            if (default_delegate_voted)
                                sum_votes += BalanceOf(current_voter);
                        }
                    }
                }
            }
            if (sum_votes > TotalSupply() / 2)
            {
                new StorageMap(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_PROPOSAL } + (ByteString)proposal_id)
                    .Put(new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME }, Runtime.Time);
                return Contract.Call(scripthash, method, CallFlags.All, args);
            }
            else
                if(sum_votes != 0)
                    throw new Exception("Not enough votes. Got "+(ByteString)sum_votes+ " votes from given array `UInt160[] voters`");
                else
                    throw new Exception("No vote counted from given array `UInt160[] voters`");
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
