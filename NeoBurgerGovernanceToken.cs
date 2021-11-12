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
        private const byte PREFIX_EXECUTED = 0x03;
        private const byte PREFIX_PAUSEUNTIL = 0x04;
        private const byte PREFIX_LOCKEDBALANCE = 0x05;
        private const byte PREFIX_LOCKEDBALANCEFROMACCOUNT = 0x06;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_TEE = default;
        private const uint DEFAULT_WAITTIME = 86400000 * 4;

        public override byte Decimals() => 8;
        public override string Symbol() => "NOBUG";
        public static UInt160 TEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });
        public static bool NotPaused() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }) < Runtime.Time;

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, DEFAULT_TEE);
        }

        public static void LockBalance(UInt160 from, BigInteger amount)
        {
            ByteString prefixLockedBalance = (ByteString)new byte[] { PREFIX_LOCKEDBALANCE };
            BigInteger oldLockedBalance = (BigInteger)Storage.Get(Storage.CurrentContext, prefixLockedBalance);
            ExecutionEngine.Assert(oldLockedBalance > amount);
            ByteString prefixLockedBalanceFromAccount = (ByteString)new byte[] { PREFIX_LOCKEDBALANCEFROMACCOUNT };
            UInt160 oldLockedAccount = (UInt160)Storage.Get(Storage.CurrentContext, prefixLockedBalanceFromAccount);
            Storage.Put(Storage.CurrentContext, prefixLockedBalance, amount);
            Storage.Put(Storage.CurrentContext, prefixLockedBalanceFromAccount, from);
            UInt160 executingScriptHash = Runtime.ExecutingScriptHash;
            ExecutionEngine.Assert(Transfer(from, executingScriptHash, amount, null));
            if (oldLockedBalance > 0)
                try
                {
                    Transfer(executingScriptHash, oldLockedAccount, oldLockedBalance, null);
                }
                finally { }
        }

        public static void ClaimLockedBalance()
        {
            BigInteger pausedUntil = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL });
            ExecutionEngine.Assert(pausedUntil > Runtime.Time);
            UInt160 fromAccount = (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_LOCKEDBALANCEFROMACCOUNT });
            ExecutionEngine.Assert(Runtime.CheckWitness(fromAccount));
            BigInteger balance = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_LOCKEDBALANCE });
            ExecutionEngine.Assert(Transfer(Runtime.CallingScriptHash, fromAccount, balance, null));
        }

        public static void SubmitApprovedExecution(UInt256 digest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(TEE()));
            StorageMap executionSubmittedTimeMap = new(Storage.CurrentContext, PREFIX_EXECUTION);
            executionSubmittedTimeMap.Put(digest, Runtime.Time);
        }

        public static void SubmitExecution(UInt256 digest)
        {
            ExecutionEngine.Assert(BalanceOf(Runtime.CallingScriptHash) > TotalSupply() / 2);
            StorageMap executionSubmittedTimeMap = new(Storage.CurrentContext, PREFIX_EXECUTION);
            executionSubmittedTimeMap.Put(digest, Runtime.Time);
        }

        public static void PauseContract()
        {
            UInt160 fromAccount = (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_LOCKEDBALANCEFROMACCOUNT });
            ExecutionEngine.Assert(Runtime.CheckWitness(fromAccount));
            BigInteger currentPauseUntil = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL });
            BigInteger balance = (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_LOCKEDBALANCE });
            BigInteger newPauseUntil = Runtime.Time + BigInteger.Pow(2, (int)(balance / TotalSupply())) * 600000;
            ExecutionEngine.Assert(currentPauseUntil > newPauseUntil);
            newPauseUntil = currentPauseUntil;
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }, newPauseUntil);
        }

        public static object Execute(BigInteger id, UInt160 scripthash, string method, object[] args)
        {
            ExecutionEngine.Assert(NotPaused());
            ByteString digest = CryptoLib.Sha256(StdLib.Serialize(new object[] { id, scripthash, method, args }));
            StorageMap executionSubmittedTimeMap = new(Storage.CurrentContext, PREFIX_EXECUTION);
            BigInteger timestamp = (BigInteger)executionSubmittedTimeMap.Get(digest);
            BigInteger currentTime = Runtime.Time;
            ExecutionEngine.Assert(timestamp + DEFAULT_WAITTIME > currentTime);
            StorageMap executedTimeMap = new(Storage.CurrentContext, PREFIX_EXECUTED);
            ExecutionEngine.Assert((BigInteger)executedTimeMap.Get(digest) > 0);
            executedTimeMap.Put(digest, currentTime);
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
