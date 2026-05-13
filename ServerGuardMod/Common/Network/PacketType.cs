namespace ServerGuardMod.Common.Network
{
    public enum PacketType : byte
    {
        // Server -> Client
        LoginRequired   = 1,
        LoginSuccess    = 2,
        LoginFail       = 3,
        SyncPlayerData  = 4,
        Kick            = 5,
        FreezePlayer    = 6,
        AdminMessage    = 7,

        // Client -> Server
        RequestLogin    = 10,
        RequestRegister = 11,
    }
}
