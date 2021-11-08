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
        private static readonly UInt160 INITIAL_HOLDER = default;
        private static readonly byte PREFIX_PROPOSAL_ID = 0x02;
        private static readonly byte PREFIX_PROPOSAL = 0x03;
        private static readonly byte PREFIX_PROPOSAL_VOTING_DEADLINE = 0x04;
        private static readonly byte PREFIX_PROPOSAL_EXECUTED_TIME = 0x05;
        private static readonly byte PREFIX_DELEGATE = 0x81;
        private static readonly byte PREFIX_DEFAULT_DELEGATE = 0x82;
        private static readonly byte PREFIX_DELEGATE_THRESHOLD = 0x83;
        private static readonly byte PREFIX_VOTE = 0xc1;
        private static readonly byte PREFIX_VOTING_PERIOD = 0xc2;

        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";
        public static BigInteger GetVotingPeriod() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_VOTING_PERIOD });
        public static BigInteger GetNextProposalID() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID });

        public static object[] ProposalAttributes(BigInteger id)
        {
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = (ProposalAttributesStruct)proposal_id_map.GetObject((ByteString)id);
            StorageMap proposal_voting_deadline_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_VOTING_DEADLINE });
            BigInteger voting_end_time = (BigInteger)proposal_voting_deadline_map.Get((ByteString)id);
            StorageMap proposal_executed_time_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME });
            BigInteger executed_time = (BigInteger)proposal_executed_time_map.Get(new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME });
            return new object[] { proposal_attributes.scripthash, proposal_attributes.method, proposal_attributes.args, voting_end_time, executed_time };
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
            public UInt160 scripthash;
            public string method;
            public ByteString[] args;
        }

        public static void _deploy(object data, bool update)
        {
            if (!update)
            {
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID }, 1);
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_VOTING_PERIOD }, 86400000 * 7);
                Mint(INITIAL_HOLDER, 10_000_000_000_000_000);
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_DEFAULT_DELEGATE }, INITIAL_HOLDER);
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_DELEGATE_THRESHOLD }, 100_000_000_000_000);
            }
        }

        public static void BecomeDefaultDelegate(UInt160 account)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(account));
            UInt160 default_delegate = (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_DEFAULT_DELEGATE });
            BigInteger to_account_balance = BalanceOf(account);
            BigInteger default_delegate_balance = BalanceOf(default_delegate);
            if (to_account_balance > default_delegate_balance && to_account_balance > GetDelegateThreshold())
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_DEFAULT_DELEGATE }, account);
            else
                throw new Exception("No enough tokens. You need "+(ByteString)default_delegate_balance+" NOBUGs to be the default delegate");
        }

        public static void SetMinimalTimePeriodForVoting(BigInteger time_period)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_VOTING_PERIOD }, time_period);
        }
        public static BigInteger NewProposal(UInt160 scripthash, string method, ByteString[] args)
        {
            BigInteger proposal_id = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID });
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID }, proposal_id + 1);
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = new();
            proposal_attributes.scripthash = scripthash;
            proposal_attributes.method = method;
            proposal_attributes.args = args;
            proposal_id_map.PutObject((ByteString)proposal_id, proposal_attributes);
            StorageMap proposal_voting_end_time_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_VOTING_DEADLINE });
            proposal_voting_end_time_map.Put((ByteString)proposal_id, Runtime.Time + GetVotingPeriod());
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
            StorageMap proposal_voting_deadline_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_VOTING_DEADLINE });
            BigInteger voting_deadline = (BigInteger)proposal_voting_deadline_map.Get((ByteString)proposal_index);
            if (voting_deadline == 0)
                throw new Exception("The proposal does not exist");
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
