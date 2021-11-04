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
        private static readonly byte PREFIX_PROPOSAL = 0x01;
        private static readonly byte PREFIX_PROPOSAL_SCRIPT_HASH = 0x02;
        private static readonly byte PREFIX_PROPOSAL_ID = 0x03;
        private static readonly byte PREFIX_PROPOSAL_METHOD = 0x04;
        private static readonly byte PREFIX_PROPOSAL_ARG = 0x05;
        private static readonly byte PREFIX_PROPOSAL_ARG_COUNT = 0x06;
        private static readonly byte PREFIX_PROPOSAL_VOTING_DEADLINE = 0x07;
        private static readonly byte PREFIX_PROPOSAL_EXECUTED_TIME = 0x08;
        private static readonly byte PREFIX_DELEGATE = 0x81;
        private static readonly byte PREFIX_DEFAULT_DELEGATE = 0x82;
        private static readonly byte PREFIX_DELEGATE_THRESHOLD = 0x83;
        private static readonly byte PREFIX_VOTE = 0xc1;
        private static readonly byte PREFIX_MINIMAL_TIME_PERIOD_FOR_VOTING = 0xc2;

        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";
        public static BigInteger MinimalTimePeriodForVoting() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_MINIMAL_TIME_PERIOD_FOR_VOTING });

        public static object[] ProposalAttributes(BigInteger id)
        {
            StorageMap proposal_map = new(Storage.CurrentContext, PREFIX_PROPOSAL + (ByteString)id);
            UInt160 scripthash = (UInt160)proposal_map.Get(new byte[] { PREFIX_PROPOSAL_SCRIPT_HASH });
            string method = proposal_map.Get(new byte[] { PREFIX_PROPOSAL_METHOD });
            BigInteger arg_count = (BigInteger)proposal_map.Get(new byte[] { PREFIX_PROPOSAL_ARG_COUNT });
            ByteString[] args = new ByteString[(int)arg_count];
            StorageMap arg_map = new(Storage.CurrentContext, PREFIX_PROPOSAL + (ByteString)id + PREFIX_PROPOSAL_ARG);
            for (BigInteger j = 1; j <= arg_count; j++)
                args[(int)j - 1] = arg_map.Get((ByteString)j);
            BigInteger voting_deadline = (BigInteger)proposal_map.Get(new byte[] { PREFIX_PROPOSAL_VOTING_DEADLINE });
            BigInteger executed_time = (BigInteger)proposal_map.Get(new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME });
            return new object[] { scripthash, method, args, voting_deadline, executed_time };
        }

        public static UInt160 GetDelegate(UInt160 from) => (UInt160)new StorageMap(Storage.CurrentContext, PREFIX_DELEGATE).Get(from);
        public static UInt160 GetDefaultDelegate() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_DEFAULT_DELEGATE });
        public static BigInteger GetDefaultDelegateBalance() => BalanceOf(GetDefaultDelegate());
        public static BigInteger GetDelegateThreshold() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_DELEGATE_THRESHOLD });
        public static bool IsValidDelegate(UInt160 account) => account != UInt160.Zero && (BalanceOf(account) > GetDelegateThreshold());
        public static BigInteger GetVote(UInt160 from, BigInteger proposal_index) => (BigInteger)new StorageMap(Storage.CurrentContext, PREFIX_VOTE).Get(from + (ByteString)proposal_index);


        public static void _deploy(object data, bool update)
        {
            if (!update)
            {
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID }, 1);
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_MINIMAL_TIME_PERIOD_FOR_VOTING }, 86400000 * 7);
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
                throw new Exception((ByteString)default_delegate_balance);
        }

        public static void SetMinimalTimePeriodForVoting(BigInteger minimal_time_period)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_MINIMAL_TIME_PERIOD_FOR_VOTING }, minimal_time_period);
        }
        public static BigInteger NewProposal(UInt160 scripthash, string method, ByteString[] args, BigInteger voting_period)
        {
            if (voting_period < MinimalTimePeriodForVoting())
                throw new Exception("Too short voting period");
            BigInteger proposal_id = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID });
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_ID }, proposal_id + 1);
            StorageMap proposal_id_map = new(Storage.CurrentContext, PREFIX_PROPOSAL + (ByteString)proposal_id);
            proposal_id_map.Put(new byte[] { PREFIX_PROPOSAL_SCRIPT_HASH }, scripthash);
            proposal_id_map.Put(new byte[] { PREFIX_PROPOSAL_VOTING_DEADLINE }, voting_period + Runtime.Time);
            proposal_id_map.Put(new byte[] { PREFIX_PROPOSAL_METHOD }, method);
            proposal_id_map.Put(new byte[] { PREFIX_PROPOSAL_ARG_COUNT }, args.Length);
            StorageMap arg_map = new(Storage.CurrentContext, PREFIX_PROPOSAL + (ByteString)proposal_id + PREFIX_PROPOSAL_ARG);
            for(BigInteger j = 1; j <= args.Length; j++)
                arg_map.Put((ByteString)j, args[(int)j - 1]);
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
            StorageMap proposal_map = new(Storage.CurrentContext, PREFIX_PROPOSAL + (ByteString)proposal_index);
            BigInteger voting_deadline = (BigInteger)proposal_map.Get(new byte[] { PREFIX_PROPOSAL_VOTING_DEADLINE });
            if (voting_deadline == 0)
            {
                throw new Exception("The proposal does not exist");
            }
            if(Runtime.Time > voting_deadline)
            {
                throw new Exception("Cannot vote after the deadline");
            }
            StorageMap vote_map = new(Storage.CurrentContext, PREFIX_VOTE);
            ByteString key = from + (ByteString)proposal_index;
            if (for_or_against)
            {
                vote_map.Put(key, 1);
            }
            else
            {
                vote_map.Delete(key);
            }
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
                throw new Exception("Cannot execute proposal after voting deadline");
            if (proposal_executed > 0)
                throw new Exception("Proposal already executed");
            BigInteger sum_votes = 0;
            BigInteger voter_count = voters.Length;
            bool default_delegate_voted = GetVote(GetDefaultDelegate(), proposal_id) > 0;
            for (BigInteger i=0;i<voter_count; i++)
            {
                UInt160 current_voter = voters[(int)i];
                UInt160 current_delegate = GetDelegate(current_voter);
                if (
                    GetVote(current_voter, proposal_id) > 0 ||
                    (IsValidDelegate(current_delegate) && GetVote(current_delegate, proposal_id) > 0) ||
                    default_delegate_voted
                )
                    sum_votes += BalanceOf(current_voter);
            }
            if (sum_votes > TotalSupply() / 2)
            {
                new StorageMap(Storage.CurrentContext, PREFIX_PROPOSAL + (ByteString)proposal_id)
                    .Put(new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME }, Runtime.Time);
                return Contract.Call(scripthash, method, CallFlags.All, args);
            }
            else
                throw new Exception((ByteString)sum_votes);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            return;
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
