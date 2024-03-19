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
        private const byte PREFIX_MINTROOT = 0x05;
        private const byte PREFIX_MINTED = 0x06;

        [InitialValue("0x82450b644631506b6b7194c4071d0b98d762771f", ContractParameterType.Hash160)]
        private static readonly UInt160 DEFAULT_TEE = default;
        private const uint DEFAULT_WAITTIME = 86400000 * 4;

        public override byte Decimals() => 10;
        public override string Symbol() => "NOBUG";
        public static UInt160 TEE() => (UInt160)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_TEE });
        public static bool NotPaused() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }) < Runtime.Time;
        public static UInt256 MintRoot() => (UInt256)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_MINTROOT });

        public static void _deploy(object data, bool update)
        {
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, DEFAULT_TEE);
        }
        public static void SubmitApprovedExecution(UInt256 digest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(TEE()) && NotPaused());
            new StorageMap(Storage.CurrentContext, PREFIX_EXECUTION).Put(digest, Runtime.Time + DEFAULT_WAITTIME);
        }
        public static void SubmitExecution(UInt160 caller, UInt256 digest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(caller) && BalanceOf(caller) * 2 > TotalSupply());
            new StorageMap(Storage.CurrentContext, PREFIX_EXECUTION).Put(digest, Runtime.Time + DEFAULT_WAITTIME / 2);
        }
        public static void BanExecution(UInt160 caller, UInt256 digest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(caller) && BalanceOf(caller) * 2 > TotalSupply());
            new StorageMap(Storage.CurrentContext, PREFIX_EXECUTED).Put(digest, -1);
        }
        public static object Execute(UInt160 scripthash, string method, object[] args, BigInteger nonce)
        {
            ExecutionEngine.Assert(NotPaused());
            ByteString digest = CryptoLib.Sha256(StdLib.Serialize(new object[] { scripthash, method, args, nonce }));
            BigInteger timestamp = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIX_EXECUTION).Get(digest);
            ExecutionEngine.Assert(timestamp > 0);
            BigInteger now = Runtime.Time;
            ExecutionEngine.Assert(timestamp < now);
            StorageMap executed = new(Storage.CurrentContext, PREFIX_EXECUTED);
            ExecutionEngine.Assert((BigInteger)executed.Get(digest) == 0);
            executed.Put(digest, now);
            return Contract.Call(scripthash, method, CallFlags.All, args);
        }
        public static void Claim(UInt160 claimer, BigInteger num, BigInteger nonce, UInt256[] proof)
        {
            const byte LEAF = 0x00;
            const byte INTERNAL = 0x01;
            UInt256 userDigest = (UInt256)CryptoLib.Sha256(StdLib.Serialize(new object[] { LEAF, claimer, num, nonce }));
            StorageMap minted = new(Storage.CurrentContext, PREFIX_MINTED);
            ExecutionEngine.Assert((BigInteger)minted.Get(userDigest) == 0);
            UInt256 digest = userDigest;
            foreach (UInt256 sibling in proof)
            {
                if ((BigInteger)digest < (BigInteger)sibling)
                    digest = (UInt256)CryptoLib.Sha256(StdLib.Serialize(new object[] { INTERNAL, digest, sibling }));
                else
                    digest = (UInt256)CryptoLib.Sha256(StdLib.Serialize(new object[] { INTERNAL, sibling, digest }));
            }
            ExecutionEngine.Assert(digest == Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_MINTROOT }));
            minted.Put(userDigest, num);
            Mint(claimer, num);
            ExecutionEngine.Assert(TotalSupply() < BigInteger.Pow(2, 64));
        }
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
        }
        public static void SetTEE(UInt160 newTEE)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_TEE }, newTEE);
        }
        public static void SetMintRoot(UInt256 root)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_MINTROOT }, root);
        }
        public static void PauseDAO(UInt160 caller)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(caller) && BalanceOf(caller) > TotalSupply() / 4);
            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PAUSEUNTIL }, Runtime.Time + DEFAULT_WAITTIME);
        }
        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(Runtime.ExecutingScriptHash));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
