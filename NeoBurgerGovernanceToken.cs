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
        private static readonly UInt160 TEE_ADDRESS = default;
        private const byte PREFIX_TEE = 0x01;
        private const byte PREFIX_PASSED_PROPOSAL = 0x02;
        private const byte PREFIX_CONTRACT_PAUSED = 0x03;
        private const uint PUBLICITY_PERIOD_BEFORE_EXECUTION = 86400000 * 3;
        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";

        public static UInt160 TEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });
        public static Iterator PassedProposals() => Storage.Find(Storage.CurrentContext, new byte[] { PREFIX_PASSED_PROPOSAL }, FindOptions.RemovePrefix);

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, TEE_ADDRESS);
        }


        public static void ProposalPassed(BigInteger proposal_id)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE })) || BalanceOf(Runtime.CallingScriptHash) > TotalSupply() / 2);
            Storage.Put(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_PASSED_PROPOSAL } + (ByteString)proposal_id, Runtime.Time);
        }

        public static void PauseContract()
        {
            BigInteger currentPauseUntil = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_CONTRACT_PAUSED });
            BigInteger newPauseUntil = Runtime.Time + BigInteger.Pow(2, (int)(BalanceOf(Runtime.CallingScriptHash)/TotalSupply())) * 600000;
            if (currentPauseUntil < newPauseUntil)
                Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_CONTRACT_PAUSED }, newPauseUntil);
            else
                throw new Exception("contract already paused until timestamp " + (ByteString)currentPauseUntil);
        }

        public static object ExecuteProposal(BigInteger proposal_id, UInt160 scripthash, string method, ByteString[] args)
        {
            if (!Runtime.CheckWitness((UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE })))
                if (BalanceOf(Runtime.CallingScriptHash) > TotalSupply() / 2)
                    return Contract.Call(scripthash, method, CallFlags.All, args);
                else
                    throw new Exception("No witness from TEE");
            ByteString proposalPassedKey = (ByteString)new byte[] { PREFIX_PASSED_PROPOSAL } + (ByteString)proposal_id;
            if (Runtime.Time - (BigInteger)Storage.Get(Storage.CurrentContext, proposalPassedKey) < PUBLICITY_PERIOD_BEFORE_EXECUTION)
                throw new Exception("not enough publicity period");
            if ((BigInteger)Storage.Get(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_CONTRACT_PAUSED }) > Runtime.Time)
                throw new Exception("contract paused");
            Storage.Delete(Storage.CurrentContext, proposalPassedKey);
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }

        public static void SetTEE(UInt160 newTEE)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash) || BalanceOf(Runtime.CallingScriptHash) > TotalSupply() / 2);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, newTEE);
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash) || BalanceOf(Runtime.CallingScriptHash) > TotalSupply() / 2);
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
