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
        private const byte PREFIX_TEE = 0x50;
        private const byte PREFIX_PASSED_PROPOSAL = 0x90;
        private const byte PREFIX_PAUSED = 0x90;
        private const uint PUBLICITY_PERIOD_BEFORE_EXECUTION = 86400000 * 3;
        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";

        public static UInt160 TEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });
        public static Iterator PassedProposals() => Storage.Find(Storage.CurrentContext, new byte[] { PREFIX_PASSED_PROPOSAL }, FindOptions.RemovePrefix);
        public static Iterator PausedProposals() => Storage.Find(Storage.CurrentContext, new byte[] { PREFIX_PAUSED }, FindOptions.RemovePrefix);

        public static void ProposalPassed(BigInteger proposal_id)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE })));
            Storage.Put(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_PASSED_PROPOSAL } + (ByteString)proposal_id, Runtime.Time);
        }

        public static void ExecutionPaused(BigInteger proposal_id)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE })));
            Storage.Put(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_PAUSED } + (ByteString)proposal_id, 1);
        }

        public static void ExecutionResumed(BigInteger proposal_id)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE })));
            Storage.Delete(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_PAUSED } + (ByteString)proposal_id);
        }

        public static object ExecuteProposal(BigInteger proposal_id, UInt160 scripthash, string method, ByteString[] args)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE })));
            ByteString proposalPassedKey = (ByteString)new byte[] { PREFIX_PASSED_PROPOSAL } + (ByteString)proposal_id;
            if (Runtime.Time - (BigInteger)Storage.Get(Storage.CurrentContext, proposalPassedKey) < PUBLICITY_PERIOD_BEFORE_EXECUTION)
                throw new System.Exception("not enough publicity period");
            if ((BigInteger)Storage.Get(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_PAUSED } + (ByteString)proposal_id) > 0)
                throw new System.Exception("proposal paused");
            Storage.Delete(Storage.CurrentContext, proposalPassedKey);
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }

        public static void ChangeTEE(UInt160 newTEE)
        {
            ByteString newTEEBytearray = (ByteString)new byte[] { PREFIX_TEE };
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, newTEEBytearray)));
            Storage.Put(Storage.CurrentContext, newTEEBytearray, newTEE);
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
