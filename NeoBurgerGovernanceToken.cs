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
        private const byte PREFIX_TEE = 0x01;
        private const byte PREFIX_EXECUTION = 0x02;
        private const byte PREFIX_PAUSEUNTIL = 0x03;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_TEE = default;
        private const uint DEFAULT_WAITTIME = 86400000 * 4;

        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";
        public static UInt160 TEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });
        public static bool Proceed() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }) < Runtime.Time;

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, DEFAULT_TEE);
        }


        public static void SubmitApprovedExecution(UInt256 digest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(TEE()));
            // TODO: RECHECK OPERATOR +
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_EXECUTION } + digest, Runtime.Time);
        }

        public static void SubmitExecution(UInt256 digest)
        {
            // TODO: CHECK BALANCE OF CALLER
            // TODO: RECHECK OPERATOR +
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_EXECUTION } + digest, Runtime.Time);
        }

        public static void PauseContract()
        {
            BigInteger currentPauseUntil = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL });
            // TODO: FIX BELOW
            BigInteger newPauseUntil = Runtime.Time + BigInteger.Pow(2, (int)(BalanceOf(Runtime.CallingScriptHash)/TotalSupply())) * 600000;
            // TODO: MIN(OLD, NEW)
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }, newPauseUntil);
        }

        public static object Execute(UInt160 scripthash, string method, object[] args)
        {
            ExecutionEngine.Assert(Proceed());
            // TODO: CALCULATE DIGEST
            BigInteger timestamp = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_EXECUTION } + digest);
            ExecutionEngine.Assert(timestamp > Runtime.Time + DEFAULT_WAITTIME);
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }

        public static void SetTEE(UInt160 newTEE)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, newTEE);
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
