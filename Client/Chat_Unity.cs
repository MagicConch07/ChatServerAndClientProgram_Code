using System;
using System.Collections.Generic;
using NetBase;

namespace MKChatServer
{
    public interface IPacketSerializable
    {
        void Serialize(PacketBase packet);
        void Deserialize(PacketBase packet);
    }
public static class Constants
{
}
public enum MsgType
{
    Msg,
    Whisper,
    
}

public enum PacketType
{
    None,
    NickNameReq,
    NickNameAck,
    MessageReq,
    MessageAck,
    Max,
}


namespace ChatPacket
{
    public struct NickNameReq
    {
        public string name;
        public void Serialize(NetBase.PacketBase PacketBase)
        {
            PacketBase.Write(name);
        }
        public void Deserialize(NetBase.PacketBase PacketBase)
        {
            name = PacketBase.Read<string>();
        }
    }
    public struct NickNameAck
    {
        public bool successful;
        public void Serialize(NetBase.PacketBase PacketBase)
        {
            PacketBase.Write(successful);
        }
        public void Deserialize(NetBase.PacketBase PacketBase)
        {
            successful = PacketBase.Read<bool>();
        }
    }
    public struct MessageReq
    {
        public string msg;
        public string senderName;
        public string receiverName;
        public MsgType msgType;
        public void Serialize(NetBase.PacketBase PacketBase)
        {
            PacketBase.Write(msg);
            PacketBase.Write(senderName);
            PacketBase.Write(receiverName);
            PacketBase.Write(msgType);
        }
        public void Deserialize(NetBase.PacketBase PacketBase)
        {
            msg = PacketBase.Read<string>();
            senderName = PacketBase.Read<string>();
            receiverName = PacketBase.Read<string>();
            msgType = PacketBase.Read<MsgType>();
        }
    }
    public struct MessageAck
    {
        public string msg;
        public string senderName;
        public string receiverName;
        public MsgType msgType;
        public void Serialize(NetBase.PacketBase PacketBase)
        {
            PacketBase.Write(msg);
            PacketBase.Write(senderName);
            PacketBase.Write(receiverName);
            PacketBase.Write(msgType);
        }
        public void Deserialize(NetBase.PacketBase PacketBase)
        {
            msg = PacketBase.Read<string>();
            senderName = PacketBase.Read<string>();
            receiverName = PacketBase.Read<string>();
            msgType = PacketBase.Read<MsgType>();
        }
    }
}

}
