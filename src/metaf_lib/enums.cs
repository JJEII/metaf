using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MetAF.enums
{
    public enum CTypeID
    {
        Unassigned = -1,
        Never = 0,
        Always = 1,
        All = 2,
        Any = 3,
        ChatMatch = 4,
        MainSlotsLE = 5,
        SecsInStateGE = 6,
        NavEmpty = 7,
        Death = 8,
        VendorOpen = 9,
        VendorClosed = 10,
        ItemCountLE = 11,
        ItemCountGE = 12,
        MobsInDist_Name = 13,
        MobsInDist_Priority = 14,
        NeedToBuff = 15,
        NoMobsInDist = 16,
        BlockE = 17,
        CellE = 18,
        IntoPortal = 19,
        ExitPortal = 20,
        Not = 21,
        PSecsInStateGE = 22,
        SecsOnSpellGE = 23,
        BuPercentGE = 24,
        DistToRteGE = 25,
        Expr = 26,
        //ClientDialogPopup = 27, // some type from the past? it's not in vt now.
        ChatCapture = 28
    };

    public enum NavTypeID
    {
        Circular = 1,
        Linear = 2,
        Follow = 3,
        Once = 4
    };

    public enum M_NavTypeID // These parallel the NavTypeID list, but are used in the metaf file, for NAV types, by NAME, not by value
    {
        circular = 1,
        linear = 2,
        follow = 3,
        once = 4
    };

    public enum NTypeID
    {
        Follow = -2, // workaround = MY VALUE FOR THIS, not VTank's!
        Unassigned = -1,
        Point = 0,
        Portal = 1, // DEPRECATED Portal node (only has one set of coordinates instead of two (like "ptl" type has)
        Recall = 2,
        Pause = 3,
        Chat = 4,
        OpenVendor = 5,
        Portal_NPC = 6,
        NPCTalk = 7,
        Checkpoint = 8,
        Jump = 9
        //Other = 99 // defined in VTank source
    };

    public enum M_NTypeID // These parallel the NTypeID list, but are used in the metaf file, for NAV node types, by NAME, not by value
    {
        flw = -2,
        Unassigned = -1,
        pnt = 0,
        prt = 1, // DEPRECATED Portal node (only has one set of coordinates instead of two (like "ptl" type has)
        rcl = 2,
        pau = 3,
        cht = 4,
        vnd = 5,
        ptl = 6,
        tlk = 7,
        chk = 8,
        jmp = 9
        // otr = 99 // "Other" is in VTank source
    };
    public enum ATypeID
    {
        Unassigned = -1,
        None = 0,
        SetState = 1,
        Chat = 2,
        DoAll = 3,
        EmbedNav = 4,
        CallState = 5,
        Return = 6,
        DoExpr = 7,
        ChatExpr = 8,
        SetWatchdog = 9,
        ClearWatchdog = 10,
        GetOpt = 11,
        SetOpt = 12,
        CreateView = 13,
        DestroyView = 14,
        DestroyAllViews = 15
    };

}