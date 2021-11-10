﻿using System;
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
    public class NeoBurgerVote:SmartContract
    {
        [InitialValue("[TODO]: ARGS", ContractParameterType.Hash160)]
        private const UInt160 OWNER = default;
        private const ulong VOTING_PERIOD = 86400000 * 7;
        private const byte PREFIX_PROPOSAL_LATEST_ID = 0x02;
        private const byte PREFIX_PROPOSAL = 0x03;
        private const byte PREFIX_PROPOSAL_PAUSED = 0x04;
        private const byte PREFIX_PROPOSAL_EXECUTED_TIME = 0x05;
        private const byte PREFIX_DELEGATE = 0x81;
        private const byte PREFIX_VOTE = 0xc1;

        public static BigInteger GetVotingPeriod() => VOTING_PERIOD;
        public static BigInteger GetLatestProposalID() => (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_LATEST_ID });

        public static object[] ProposalAttributes(BigInteger id)
        {
            StorageMap proposal_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = (ProposalAttributesStruct)proposal_map.GetObject((ByteString)id);
            StorageMap proposal_executed_time_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME });
            BigInteger executed_time = (BigInteger)proposal_executed_time_map.Get((ByteString)id);
            StorageMap proposal_paused_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_PAUSED });
            BigInteger paused = (BigInteger)proposal_paused_map.Get((ByteString)id);
            return new object[] { proposal_attributes.provider, proposal_attributes.scripthash, proposal_attributes.method, proposal_attributes.args, proposal_attributes.created_time, proposal_attributes.voting_deadline, paused, executed_time };
        }

        public static UInt160 GetDelegate(UInt160 from) => (UInt160)new StorageMap(Storage.CurrentContext, PREFIX_DELEGATE).Get(from);
        public static BigInteger GetVote(UInt160 from, BigInteger proposal_index) => (BigInteger)new StorageMap(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_index).Get(from);
        public static Iterator GetVotersOfProposal(BigInteger proposal_id) => new StorageMap(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_id).Find((FindOptions)((byte)FindOptions.KeysOnly + (byte)FindOptions.RemovePrefix));

        struct ProposalAttributesStruct
        {
            public BigInteger id;
            public UInt160 provider;
            public UInt160 scripthash;
            public string method;
            public ByteString[] args;
            public BigInteger created_time;
            public BigInteger voting_deadline;
        }

        public static void _deploy(object data, bool update)
        {
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            ProposalAttributesStruct proposal_attributes = new();
            proposal_attributes.id = 0;
            proposal_id_map.PutObject((ByteString)(BigInteger)0, proposal_attributes);
        }

        public static void SetEmergencyPause(BigInteger proposal_id, bool pause)
        {
            StorageMap proposal_paused_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_PAUSED });
            if (pause)
            {
                StorageMap proposal_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
                ProposalAttributesStruct proposal_attributes = (ProposalAttributesStruct)proposal_map.GetObject((ByteString)proposal_id);
                ExecutionEngine.Assert(Runtime.CheckWitness(OWNER) || Runtime.CheckWitness(proposal_attributes.provider));
                proposal_paused_map.Put((ByteString)proposal_id, 1);
            }
            else
            {
                ExecutionEngine.Assert(Runtime.CheckWitness(OWNER));
                proposal_paused_map.Put((ByteString)proposal_id, 0);
            }
        }

        public static BigInteger NewProposal(UInt160 provider, BigInteger proposal_id, UInt160 scripthash, string method, ByteString[] args)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(provider));
            StorageMap proposal_id_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL });
            if ((BigInteger)proposal_id_map.Get((ByteString)proposal_id) != 0 || ((ProposalAttributesStruct)proposal_id_map.GetObject((ByteString)(proposal_id - 1))).id != proposal_id - 1)
                throw new Exception("Invalid proposal id");

            Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_LATEST_ID }, (ByteString)proposal_id);
            
            ProposalAttributesStruct proposal_attributes = new();
            proposal_attributes.id = proposal_id;
            proposal_attributes.provider = provider;
            proposal_attributes.scripthash = scripthash;
            proposal_attributes.method = method;
            proposal_attributes.args = args;
            BigInteger current_time = Runtime.Time;
            proposal_attributes.created_time = current_time;
            proposal_attributes.voting_deadline = current_time + VOTING_PERIOD;
            proposal_id_map.PutObject((ByteString)proposal_id, proposal_attributes);

            StorageMap proposal_paused_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_PAUSED });
            proposal_paused_map.Put((ByteString)proposal_id, 0);

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
            if (Runtime.Time > voting_deadline)
                throw new Exception("Cannot vote after the deadline");
            StorageMap proposal_executed_time_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME });
            BigInteger executed_time = (BigInteger)proposal_executed_time_map.Get((ByteString)proposal_index);
            if (executed_time > 0)
                throw new Exception("Cannot vote for executed proposal");
            StorageMap proposal_paused_map = new(Storage.CurrentContext, new byte[] { PREFIX_PROPOSAL_PAUSED });
            BigInteger paused = (BigInteger)proposal_paused_map.Get((ByteString)proposal_index);
            if (paused > 0)
                throw new Exception("Cannot vote for paused proposal");
            StorageMap vote_map = new(Storage.CurrentContext, (ByteString)new byte[] { PREFIX_VOTE } + (ByteString)proposal_index);
            if (for_or_against)
                vote_map.Put(from, 1);
            else
                vote_map.Delete(from);
        }

        public static void MarkProposalExecuted(BigInteger id, BigInteger time)
        {
            ByteString proposal_executed_time_bytearray = (ByteString)new byte[] { PREFIX_PROPOSAL_EXECUTED_TIME };
            StorageMap proposal_executed_time_map = new(Storage.CurrentContext, proposal_executed_time_bytearray);
            proposal_executed_time_map.Put((ByteString)id, (ByteString)time);
        }

        public static void Update(ByteString nefFile, string manifest)
        {
            ExecutionEngine.Assert(Runtime.CheckWitness(OWNER));
            ContractManagement.Update(nefFile, manifest, null);
        }
    }
}
