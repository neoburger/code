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
        private const byte PREFIX_ROOT = 0x05;
        private const byte PREFIX_AIRDROPPED = 0x06;

        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_TEE = default;
        private const uint DEFAULT_WAITTIME = 86400000 * 4;

        public override byte Decimals() => 10;
        public override string Symbol() => "NOBUG";
        public static UInt160 TEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });
        public static bool NotPaused() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }) < Runtime.Time;

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, DEFAULT_TEE);
        }
        public static void SubmitApprovedExecution(UInt256 digest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(TEE()));
            ExecutionEngine.Assert(NotPaused());
            new StorageMap(Storage.CurrentContext, PREFIX_EXECUTION).Put(digest, Runtime.Time + DEFAULT_WAITTIME);
        }
        public static void SubmitExecution(UInt256 digest)
        {
            ExecutionEngine.Assert(BalanceOf(Runtime.CallingScriptHash) * 2 > TotalSupply());
            new StorageMap(Storage.CurrentContext, PREFIX_EXECUTION).Put(digest, Runtime.Time + DEFAULT_WAITTIME / 2);
        }
        public static object Execute(UInt160 scripthash, string method, object[] args, BigInteger nonce)
        {
            ExecutionEngine.Assert(NotPaused() || BalanceOf(Runtime.CallingScriptHash) * 2 > TotalSupply());
            ByteString digest = CryptoLib.Sha256(StdLib.Serialize(new object[] { scripthash, method, args, nonce }));
            BigInteger timestamp = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIX_EXECUTION).Get(digest);
            BigInteger now = Runtime.Time;
            ExecutionEngine.Assert(timestamp < now);
            StorageMap executed = new(Storage.CurrentContext, PREFIX_EXECUTED);
            ExecutionEngine.Assert((BigInteger)executed.Get(digest) == 0);
            executed.Put(digest, now);
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }
        public static void Claim(BigInteger num, UInt256[] proof)
        {
            UInt160 caller = Runtime.CallingScriptHash;
            StorageMap airdropped = new(Storage.CurrentContext, PREFIX_AIRDROPPED);
            ExecutionEngine.Assert((BigInteger)airdropped.Get(caller) == 0);
            const byte leafPrefix = 0x00;
            const byte nonLeafPrefix = 0x01;
            UInt256 hash = (UInt256)CryptoLib.Sha256(StdLib.Serialize(new object[] { leafPrefix, caller, num }));
            int proofLength = proof.Length;
            UInt256 sibling;
            while(proofLength > 0)
            {
                proofLength -= 1;
                sibling = proof[proofLength];
                if((BigInteger)hash < (BigInteger)sibling)
                    hash = (UInt256)CryptoLib.Sha256(StdLib.Serialize(new object[] { nonLeafPrefix, hash, sibling}));
                else
                    hash = (UInt256)CryptoLib.Sha256(StdLib.Serialize(new object[] { nonLeafPrefix, sibling, hash }));
            }
            ExecutionEngine.Assert(hash == Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_ROOT }));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_AIRDROPPED }, num);
            Mint(caller, num);
        }
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }
        public static void SetTEE(UInt160 newTEE)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, newTEE);
        }
        public static void SetRoot(UInt256 root)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_ROOT }, root);
        }
        public static void PauseDAO()
        {
            ExecutionEngine.Assert(BalanceOf(Runtime.CallingScriptHash) > TotalSupply() / 4);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }, Runtime.Time + DEFAULT_WAITTIME);
        }
        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
