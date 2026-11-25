#nullable enable
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Meshing;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Verifies per-brick preview mesh SHA-256 hashes match the pre-extraction baseline and that meshing has no graphics dependency.</summary>
public sealed class BrickMeshingHashParityTests
{
    private static readonly BrickShape[] _anyShapes =
    [
        BrickShape.Block,
        BrickShape.Slab,
        BrickShape.Stair,
        BrickShape.Slope,
        BrickShape.Column,
        BrickShape.Plate,
    ];

    private static readonly Orientation _rotated = new(pitch: 1, yaw: 2, roll: 0);

    private static readonly Dictionary<string, string> _expectedHashes = new(StringComparer.Ordinal)
    {
        ["wb:caution_panel|Block|id"] = "3F17755C536F538F392DF1992A9213A6B97AF5E7BEE107D84574F4724C1DE8A8",
        ["wb:caution_panel|Block|rot"] = "3F17755C536F538F392DF1992A9213A6B97AF5E7BEE107D84574F4724C1DE8A8",
        ["wb:caution_panel|Slab|id"] = "9ACBD279374B583C82B1F3360CC0D4FCA94FC2ADA95DB8A2AA139A5CDFC922DD",
        ["wb:caution_panel|Slab|rot"] = "DBCF84B4E5D30F222A8D5CCBDD578FAA418CCD1CC199C9FB1D0427C92A5C6777",
        ["wb:caution_panel|Stair|id"] = "74B591250CACA0C63E54841788120EAB15D41571903D322DDE2584D6CA9A52C8",
        ["wb:caution_panel|Stair|rot"] = "F03D93C80313DF491F0B9F456A473B17AF7A746DC923D277ACE3CADD82684740",
        ["wb:caution_panel|Slope|id"] = "F762BEE1121CFF13CB1ACA93138CBB2804D1A38ED19ED8A2B93D835A8C66FB6B",
        ["wb:caution_panel|Slope|rot"] = "8561C457F9A20CB4BC12846A7AF53BBCEB14BBA69124A3C4824757DFBAFE64F7",
        ["wb:caution_panel|Column|id"] = "2D89E5F614AFF3A2E105744CF038B96BA510194F107068EFB223A38AD15B4B1A",
        ["wb:caution_panel|Column|rot"] = "C55CA2101DCD85478456AD45E8C34B2112E6296FB0E6A396ED81642B7ADCC16F",
        ["wb:caution_panel|Plate|id"] = "7267341E141582DE9FCAABC1794744909F854F5C418D25BED4A244E10718CA85",
        ["wb:caution_panel|Plate|rot"] = "9E5AB80216AA39F74116DD968B137FEA75EFFAF710E1F0739305FE00E92A920F",
        ["wb:control_buttons|Block|id"] = "BF74AF875E534B1D83894921561E47643E9EDF9CD459C7ECF421F03B0D90567A",
        ["wb:control_buttons|Block|rot"] = "A7F9475A200FF0D15CA21EC808138805C0F6C94CA7BC11A7C8A783B4F5E610AC",
        ["wb:control_buttons|Slab|id"] = "723A147A5E80919E667778B4C24CFB551983FB7C47A57F5B5275A71626F4D321",
        ["wb:control_buttons|Slab|rot"] = "A6899B74FB99B00F3285A77CBC13A9528B94FDBCE32908235C8DA8C016CD2FD2",
        ["wb:control_buttons|Stair|id"] = "B939AC102A877AC149C9EE85E8E82665C3CA085BACCDB72F3E7DED64F578120A",
        ["wb:control_buttons|Stair|rot"] = "8DA3788078FA2C4AC7B65988A551F7EE43740B0FCEBA2FEF5D8DC832CE43FD98",
        ["wb:control_buttons|Slope|id"] = "0C5D24D2DDC25FD5B2C3D34AF8135D54D624668293C11B62AC137F4E3E99D210",
        ["wb:control_buttons|Slope|rot"] = "148ED781A2F9965E4EE6813AF4CB83E6A721E822E6B5F5C3D2AAD6CB30C8E9F8",
        ["wb:control_buttons|Column|id"] = "0AD10F84844E1A1F3025FCB36EEB525D26210019781CDC1927A51C44229E4D7B",
        ["wb:control_buttons|Column|rot"] = "AE171CFF10511F26D63492283A67E86BD6CD769CCD388E84B56817B0B1F9E8BE",
        ["wb:control_buttons|Plate|id"] = "28CF191A58582D8B2FEFCC6422516EBA846D0CC18DFF37437767CA158D2D05BE",
        ["wb:control_buttons|Plate|rot"] = "E65F723B8AAC9BA8301C6CFE312FCF4C3C876B085477434B84821AFF3B401632",
        ["wb:control_panel|Block|id"] = "6E665D6851EFA66707C802CC202E33784DADD7AD91C99BF58FD21F9E960E358B",
        ["wb:control_panel|Block|rot"] = "DE67C0BAAD8869BCFCE9B69CC4EAB0307944255DC28A0F53760E05A57A719D7B",
        ["wb:control_panel|Slab|id"] = "F095BB11E90CCBFC87A3CE325D0192D59D49F6F04CA9E5EECCA6CB838D6F8BE8",
        ["wb:control_panel|Slab|rot"] = "24B34FF77BF2D39BAE084A87FE4A94FDFDD4237A30607C4428F48F45DC353DC2",
        ["wb:control_panel|Stair|id"] = "69F77DC326F92A4A87FAC9F18648D5F498BB8B944888CF6241DE0147DA1157EE",
        ["wb:control_panel|Stair|rot"] = "FE37ED134365366C14024F79CB73BF09DA65344AEC1C41B88F93142D0B12E946",
        ["wb:control_panel|Slope|id"] = "EE9B35C1B91D4D38AFA7A1DAD0A64AEC4D8A8167247050325DA81BF3212E4DEA",
        ["wb:control_panel|Slope|rot"] = "2B72CD08A59A1A06661433457102EEE1BF238C869BF980A7E7B92DFB5DB20CC2",
        ["wb:control_panel|Column|id"] = "90B50434E8AFCB2EDA292C885B66A9BC5E396816B07C5FF0749BCCA96D207E5D",
        ["wb:control_panel|Column|rot"] = "35FB1E86F2A17BC8DD13CD74939BECD02891543E6AF3EB37920A4466EF78E619",
        ["wb:control_panel|Plate|id"] = "0361044B34CFB58F1947ACF20B7778B35CA24F98421A9E3BBA2BFD3E305EE56C",
        ["wb:control_panel|Plate|rot"] = "D3DC8A0B4F2F20E5AA840BC04A9A089AD5E9D28995462BECB9661BFC0F6C5F03",
        ["wb:core|Block|id"] = "4303A104B090AE48ABCA7D6197D74929D377AD5117BA006A11F81CAD3598BA7B",
        ["wb:core|Block|rot"] = "4303A104B090AE48ABCA7D6197D74929D377AD5117BA006A11F81CAD3598BA7B",
        ["wb:display_console|Block|id"] = "555B436D358107A7F8EBFA6D8D4FDF3C106FA4056B4A552C0345609910F99EF7",
        ["wb:display_console|Block|rot"] = "074BCF3F5CC77AEA96A6E7051467F1D50C986132AE131E91F486C1D2EACC4ACC",
        ["wb:display_console|Slab|id"] = "49C66967BA1C2C5986549F507EF4B792874CFE9F8C7CA2F046EE647B6B925CDF",
        ["wb:display_console|Slab|rot"] = "9DB1DB9B736B899310690878FDDDD9B28212F1E268AA204B97641BD28B467F95",
        ["wb:display_console|Stair|id"] = "2D5E94CAAB298B50DF9DC53BFC6C1F7B37B19214C676DBDA3DF81C5BDABA042D",
        ["wb:display_console|Stair|rot"] = "43B818EED2695A9F9271968884CB11586AF5DC6BB6F9CD24DA952EDF0F49F6D9",
        ["wb:display_console|Slope|id"] = "7F20D4761E44DBF70E8D46CE3331DF06109C404DD61ADA461CC7DBD27FDD53ED",
        ["wb:display_console|Slope|rot"] = "229E26398A11B5BCC5DDA4D6B812F34D1A051C55FBF2E86C303D7DF83171D678",
        ["wb:display_console|Column|id"] = "C483E189E62A1A53E165F605D6E95DC22C29216AF97ED39C010B536E55716B87",
        ["wb:display_console|Column|rot"] = "5E4FF2F9494B4AF4D6D870170E337C183ABC233B1AE1C81E689DBDEAC39138E4",
        ["wb:display_console|Plate|id"] = "B63E10F0B23888C4A08030BE84E78E161035CAC74F426E7FA8E0159C921B6658",
        ["wb:display_console|Plate|rot"] = "C16C8050FE5EC3BE24983BFA3ABA75CA9A8C2A7152B3BFD8457D16EBE1C0C8CA",
        ["wb:display_control|Block|id"] = "1665861E652E17D7AE24246C04EF5F25782BCAAE43175A0B6F4CEDA9895F5A61",
        ["wb:display_control|Block|rot"] = "4D47B5DACB92DD5E879FA9793110A11DBE51CAF0B7AC554D5BDE895B62E6B867",
        ["wb:display_control|Slab|id"] = "A011CB311D652ED83C4306D7AE43123462535F200D579D3CABF9387D9E20094E",
        ["wb:display_control|Slab|rot"] = "8C3C993BCF7224EF161CA02429B81624FD2DFA871B30B38D1AC2644E035D0685",
        ["wb:display_control|Stair|id"] = "B6C1B8556B6C310B8DB16200338964EBB996B2E3B9162DFEB3D512AADE3CB666",
        ["wb:display_control|Stair|rot"] = "1615EF3FD04C8ADF4E4D3C3C622AE6181EC4084F7AEE81840BDB32F8A1340691",
        ["wb:display_control|Slope|id"] = "B4D1C5BB6818E4A8F4F57BB4956C8DDA38802DE01B7B11E944893D59D92EFD94",
        ["wb:display_control|Slope|rot"] = "076D7DC633B48FB2CF801FEA2499CB47049CCED805831B235C36AF145731B14B",
        ["wb:display_control|Column|id"] = "02B1075055FBC7CC2B7143BBF41D97DA5858B92E46895FC2130AB76372A29D68",
        ["wb:display_control|Column|rot"] = "C2C5C3F20754A152937A553E989AB9D80D89125D6257C7D7D10B2F065A9022A6",
        ["wb:display_control|Plate|id"] = "614AA6CD4D571A52D3972D82414B103F290DE678D04844891B02FE26FCFBF1F4",
        ["wb:display_control|Plate|rot"] = "AF867271BD688F850393BA0AAD5E6FDF661A4B632834119766996A8E8A2DB120",
        ["wb:display_monitor|Block|id"] = "3FFD18949D74C1DA65E11C7837BE894025A7D916A899B3B1D1A48A06A66F8B74",
        ["wb:display_monitor|Block|rot"] = "947BE0C083D1E88ADCBD19BE967685A11B36A19927F1F6E7663B660B8E1AD958",
        ["wb:display_monitor|Slab|id"] = "757A8D2C982DF9C8AFD0F99B94FC1F5B4AC0F3F9EC0634CC6F8A6DCBDA2A23CA",
        ["wb:display_monitor|Slab|rot"] = "F5875C08B54B4D2409F0C16896D313C40D6BC9E950A60792D176841D6176BFBE",
        ["wb:display_monitor|Stair|id"] = "70E81EB775205A4DA8DBD7318B59AFB9EBB706D6D03D4646709C94D325F0086C",
        ["wb:display_monitor|Stair|rot"] = "1FB0BD68098D2862EECD47C2526653CE8519EA2E24C36A7CB433AA50478AC9A2",
        ["wb:display_monitor|Slope|id"] = "3922C3C01EC6FE15808B67D3707C9DA1EBE317CB9379A2A2900D5873A0FB1803",
        ["wb:display_monitor|Slope|rot"] = "71CF6175432927A68CB8BEB067DA81A1EDC0EC207C7767DF5176ADE48277762A",
        ["wb:display_monitor|Column|id"] = "E6C5978EF30F4D803B1FA7C70DDC3F0F8A6582263C731FFD1F8A2CA6F629DAE3",
        ["wb:display_monitor|Column|rot"] = "A709601777B8B96B3E01A589E2CB95000E5E6A997593E66F7FEF2A6954CD5979",
        ["wb:display_monitor|Plate|id"] = "ADD6A2089BBA1B947B58BF93399FF8982E8376B6766E499508513515290A3A71",
        ["wb:display_monitor|Plate|rot"] = "7FA804ED60D46F6D2A10CF2A3818D8CF4D51F41362A1623255AEA67DA9786E78",
        ["wb:glass|Block|id"] = "15FBF004DE492D6B752F5DA03D959ABEF2645C5F4CECC1084BCA7F8158A0E2DB",
        ["wb:glass|Block|rot"] = "15FBF004DE492D6B752F5DA03D959ABEF2645C5F4CECC1084BCA7F8158A0E2DB",
        ["wb:glass|Slab|id"] = "7CF53E2128136CADAB7B3BF8510E9047B4B68C9891A31C7F6F4668ECEA9A73D4",
        ["wb:glass|Slab|rot"] = "6309178818ABD59BCC35A7035F51E00713C2458CD90C40D07D50A14C019D539D",
        ["wb:glass|Stair|id"] = "4B748EE1F384F7FFD0FF592A143C766A2C1E71800FDC2ED4EDDF2E5BBC2709FE",
        ["wb:glass|Stair|rot"] = "6B7A590224B3BC0745A40CC82603EAE353B9DB7DE3169A096725E128BC0833FB",
        ["wb:glass|Slope|id"] = "AAF77D3C5DB0907A6B0F0BE88E4F4E8F21BA792C9FB3CE28C9418B0E4D7E1A0D",
        ["wb:glass|Slope|rot"] = "22852B6BF0B82AD589FAF1BABDFA921578BCAAE89EA6635AA762B3C3EF13EEA0",
        ["wb:glass|Column|id"] = "FAA12D6BA5C1310BCABA71A91A451D215B73BB1DECF7A17D05A138BF8CD7E2F3",
        ["wb:glass|Column|rot"] = "A5AEADBE1FBE9F0FB1EACADE357D567E7618177E87F761111BD3C35FA13AAA39",
        ["wb:glass|Plate|id"] = "0453FADAC04873911508A94042533A256E5D2095E6480FAA3A9091E81DB18F9B",
        ["wb:glass|Plate|rot"] = "36648BC96A03977F293F16C7E5F4CB6FEE82EBAB743AF17845DD75164D44CFE7",
        ["wb:grate|Block|id"] = "DD6721645D30C02E01D95AC7C979E1281198DF68F0469F2A0C287F3B434692D4",
        ["wb:grate|Block|rot"] = "DD6721645D30C02E01D95AC7C979E1281198DF68F0469F2A0C287F3B434692D4",
        ["wb:grate|Slab|id"] = "AA3F9C13DEEA6957B17E2E7B1DEB724C9E4C5B3E989D0DFD3A22CC06C403EB10",
        ["wb:grate|Slab|rot"] = "6B7D41594FF591F8F1058C01BCC5C89061E0E43E4C3716F193DADD2B47E35E5D",
        ["wb:grate|Stair|id"] = "27709345F9F7354095D77B44973AED8FA73624D0299B182177D1301AEA7B21F3",
        ["wb:grate|Stair|rot"] = "18827B91A9B2684541A54C4B69E9ADD67C92DE17DD126AB9FB213CF7A8429FD3",
        ["wb:grate|Slope|id"] = "B4D8BC7F85982498E225A70925F34CCBD04DD1491520D93EA0D960F40D9A95CF",
        ["wb:grate|Slope|rot"] = "C82CDDB03328FD6DA75C8CC1DF8B7F1281FDBAD29C5E1576A01DC034CDB8F166",
        ["wb:grate|Column|id"] = "1A27F8634132D7D886D1937A446AFCFDA2F43DD8896BC92AFE2AF50801E7990B",
        ["wb:grate|Column|rot"] = "CBFE3F1021D9CBF1546304AE5EA69F2A7571D8488F72E5398B4213222B4708B9",
        ["wb:grate|Plate|id"] = "4200BFB079173A4ADE7D87AC28F1B0B0BDFF28D3A68E6B0F026923EDC3588B20",
        ["wb:grate|Plate|rot"] = "ACE6A9D4DF34FF27C4683671316A3968F25E5F589E64B9C18C4B3B6B7B9E5444",
        ["wb:ice|Block|id"] = "470590C0E022962B3799E782CE3D84A41641FEEF3402A24A21B9B2906BDA4A06",
        ["wb:ice|Block|rot"] = "470590C0E022962B3799E782CE3D84A41641FEEF3402A24A21B9B2906BDA4A06",
        ["wb:ice|Slab|id"] = "538F502441ABEF02839C12B0C0E17AAC3495BC418BC148C7C632E5627F0252CC",
        ["wb:ice|Slab|rot"] = "9D10DE23EB56D2CA8B260AD11690A035A526C919397D829A7D587D09B0275308",
        ["wb:ice|Stair|id"] = "7CF7CCCF4BD1A6F9C638433F31496D62171636DD5CE186D007C847E75C537DFE",
        ["wb:ice|Stair|rot"] = "2B26A4669E9031F4FDDF6E5BC2987B32EA857BA15DC435A32382BC9E3DAE4E0A",
        ["wb:ice|Slope|id"] = "0DC2A42E532DF4DA33F3EC83317E30EDEEB886CA6151A53762FC183196F38AFD",
        ["wb:ice|Slope|rot"] = "21C30025D80A5B2B8EE9A72E19D67F46C1E3A48BBDD764A59BA467D10C5FEC28",
        ["wb:ice|Column|id"] = "B5F81296E7848065725D3FE8DA3FC080A90B5A8E6E3ECAF2EBE225EEA142C1BB",
        ["wb:ice|Column|rot"] = "B2732851A99FCA7AD00578BE25F3D906684CA3283A57BA80B865D9D050E1C1A2",
        ["wb:ice|Plate|id"] = "94474D817ABD95126FA18C5D1F0756AD6C94EC72398731CB140BC5FD88962FA5",
        ["wb:ice|Plate|rot"] = "669E6B6FEB68102F98403CC7A33243F5808E870593931818FD465373B9FC246A",
        ["wb:light|Block|id"] = "74E976585A186835BB95BA6A3AFE8CA5C3BACC61B743F2B3287F0679078FBBCD",
        ["wb:light|Block|rot"] = "74E976585A186835BB95BA6A3AFE8CA5C3BACC61B743F2B3287F0679078FBBCD",
        ["wb:light|Slab|id"] = "DAAB596EF1FE2DD3B0E020080D5DC2563567155626A0D45FD517BFF96DAABFBF",
        ["wb:light|Slab|rot"] = "4DC6A2608FBAA85F173D3669B714DDA06214D999CBBDD4A202773248988090D5",
        ["wb:light|Stair|id"] = "FDE61E5B34A15E4FCD316ACCB8AAC59265827BAD21E3BAB859F4B8C365CA2749",
        ["wb:light|Stair|rot"] = "381E6B73A4953EE7DEAD5DB4F968E69C62D38BBA2D7407CDB21CD6485BA8DE5A",
        ["wb:light|Slope|id"] = "24BD32F8300FD8C99C6ABDD393E9883940EE06068B234C913E7F9E0E6E8C1B5E",
        ["wb:light|Slope|rot"] = "41123FC51FB76907671AE0597785193836B708A7609433C26629928016E9DD27",
        ["wb:light|Column|id"] = "0FF097AA38F5912BBD7E3C594AB2FD7B6A3376D153F37EF3E496281C99F931D7",
        ["wb:light|Column|rot"] = "76CAA18FFF2A92ABA21F3A2ECCABB5AF44CCB796871DF2EA19BB7F6A754A71BF",
        ["wb:light|Plate|id"] = "5CF55AFC26302AAA32D5B977AB4E5709E55FF2715F843B58022CBB8AE9B7CBDD",
        ["wb:light|Plate|rot"] = "67651412FEFFB8469D5C408ED414D2BB39844DEFA474B73695C75A424D5ECD57",
        ["wb:panel|Block|id"] = "6C6987E3098C86C80721858C63ECF8DD067708CA5F701A699CB90B88A8DAC22A",
        ["wb:panel|Block|rot"] = "6C6987E3098C86C80721858C63ECF8DD067708CA5F701A699CB90B88A8DAC22A",
        ["wb:panel|Slab|id"] = "CCDCCD29D1066984850E305497F7B1F5010584042CC4837060DB7DB1405E7F4F",
        ["wb:panel|Slab|rot"] = "08D8B1E6A44E879A719A033A8D7BFC8F96AD91593771DB6C5EA6806D0980CF11",
        ["wb:panel|Stair|id"] = "94CD393CF67BAE6CC68A5B049B0A4EC9256F56442E5CDAC667A5951E922DE304",
        ["wb:panel|Stair|rot"] = "BAB7FF0C2276767D77FC974F3D4BDBD89F7EB719632E959864FAEB9E753642F6",
        ["wb:panel|Slope|id"] = "95BE9DE7A8D0D6BD048E2FC3A45893FB86D19E588448C221E39DD9456C95BD31",
        ["wb:panel|Slope|rot"] = "567C02CE920B23B14573BC9A74AA9ABF0F6B36888E7928A008353D0B115C0C9F",
        ["wb:panel|Column|id"] = "91E4815577F6315E1A6918F53DB6E0EE8756B4C2C2ABCF33870EBEBB230276F1",
        ["wb:panel|Column|rot"] = "3769F232E38065FA8DE4C7A228AE4F845248190D9392FDF35AFD2CC22CAF5A03",
        ["wb:panel|Plate|id"] = "B02A5E370572CBEAD465FDBDD447FDCC19354BF6ECF4D0E10E30D674C1DDBEEE",
        ["wb:panel|Plate|rot"] = "8B37066A102C9A48F586E388E73C5B67F8CED22383E5DFD0ACBFB615AB9FB3EB",
        ["wb:porthole|Block|id"] = "33326AF393DBB66EB970692FC090ACAF3AD95A9E1927116B446FE1C237AE1C2B",
        ["wb:porthole|Block|rot"] = "33326AF393DBB66EB970692FC090ACAF3AD95A9E1927116B446FE1C237AE1C2B",
        ["wb:porthole|Slab|id"] = "0CD14DFBC7687B71342F23039F563C2A24F1E72EF127D38030759928558BD34C",
        ["wb:porthole|Slab|rot"] = "538EB986E426B90C5A0B7992B5B432ED1E43C0D6E5F3005B773BBCA8E0E55687",
        ["wb:porthole|Stair|id"] = "C712282617BDEBA110146350EA1BAFF43EE45B39CA3F943E404C75B13B943C11",
        ["wb:porthole|Stair|rot"] = "A21B9E67A3E570BB443AB745A4F929F24F25E2D37130BBA959F61A00B5553E80",
        ["wb:porthole|Slope|id"] = "3D949EFC90E4C89C3FC8EAAB138B37AC17DFF2E79EAC9BA19CBCFACA3A2DDBF5",
        ["wb:porthole|Slope|rot"] = "CB08423AE42C57AEE7926E26E227D0C503AB36BD57CA520FDD587D6675E2AF03",
        ["wb:porthole|Column|id"] = "E40FDE33430802EB3524D2D691EE9F18C7676B8154A42BF3919037EBB4AC1FC5",
        ["wb:porthole|Column|rot"] = "4C9393C9EDE36931A3D64B448171BBF533FE95E7415718873DFC35483390317E",
        ["wb:porthole|Plate|id"] = "CDB1ED5394FC531E694C4B0FAD6A29E44DAB597977E207A90842B5F7EE42312C",
        ["wb:porthole|Plate|rot"] = "C2B977B61AC210C1DF3328EC922980F6DFACE579B5B78843CC2EF3C3CE68DBA4",
        ["wb:rock|Block|id"] = "F6E60CF0B8B4856E68DE5EA08F5A9AE450D339AD9EC5A421CAD1021FE16C874F",
        ["wb:rock|Block|rot"] = "F6E60CF0B8B4856E68DE5EA08F5A9AE450D339AD9EC5A421CAD1021FE16C874F",
        ["wb:rock|Slab|id"] = "561E18E1DE03263F39D7CB5070C8226417855D59EFEC43CB1EADBA1538251BE9",
        ["wb:rock|Slab|rot"] = "C93E7B2A279CDE9C9394C7BB615E7BCFA0A893E1EADA116FE7ED704EA541B7CC",
        ["wb:rock|Stair|id"] = "B8BC8E9D8B42C2624BFAEE9EA7E00039FE29367D9BCF61AC1A97677DFBBE4EFA",
        ["wb:rock|Stair|rot"] = "06D22EF56D5C785CFA542AAF64177FC83AAE0B8C450366037677948D95F8D3C2",
        ["wb:rock|Slope|id"] = "D4E0C98C4EB2E461867477B5FA76E1975D9989CCB96C084E9A464BC647969E07",
        ["wb:rock|Slope|rot"] = "6F4009296C571D8305022843111A21BA27FA9AE0DBBF34769215A5400441BFEB",
        ["wb:rock|Column|id"] = "7C4E9F67F1233829BC9166A30961870E7D5DF09D7D2F666D14244A5017316930",
        ["wb:rock|Column|rot"] = "48CFC4597921154562D5DBC6D217011D032A9AC8649451EC409D2EA9E9714398",
        ["wb:rock|Plate|id"] = "3A5B791CCF4B92919F3D05F52199FEA679635A0D41E2C8D093BD975CDAF074A6",
        ["wb:rock|Plate|rot"] = "233FB281BA820F05B191FFA8271CE5610EF26BEF4E6624FB1A83307B02738708",
        ["wb:small_light|Block|id"] = "4E12FA308CF3ABE0E0EA7687A4D65EBAE80B8D8F32936560AF336EE07982BE3B",
        ["wb:small_light|Block|rot"] = "4E12FA308CF3ABE0E0EA7687A4D65EBAE80B8D8F32936560AF336EE07982BE3B",
        ["wb:small_light|Slab|id"] = "EF130F670AA8F459257C030FBEA3075FC59F63E64E86DBE43110E82D9B8135BF",
        ["wb:small_light|Slab|rot"] = "D6B49189AC72BB730BD6843C5E79CF4D5B523C07A643DB01D80D09BBEA00B4CE",
        ["wb:small_light|Stair|id"] = "B4B1260D012FAF98B447AFCA1B1EE254E8B54EF5A5DCD677229AC792AFDDC52D",
        ["wb:small_light|Stair|rot"] = "1481373D4D756B33FFFFC9C001F4047203FB6D1D394B43846C68D568BC575CE6",
        ["wb:small_light|Slope|id"] = "986DBFA3DC215F4AC536547F1439CDB1B76261276D32E3A86CFA3181A07EEEEC",
        ["wb:small_light|Slope|rot"] = "471F6847C74E95482894CAACD24E9306DC411CF4D1CF049F862473072AFDB528",
        ["wb:small_light|Column|id"] = "A53E384501230B32142E2443A7FE741137C3E0C636B8CB31DA3D5426E7C4A696",
        ["wb:small_light|Column|rot"] = "1A0F36B57D3F001177A45E007DB86D0BC6EFA738C2636A113A8E8C200D1FEDF5",
        ["wb:small_light|Plate|id"] = "C2E69A751D27C9F1DD1CF32647C34295F216C0F398F2E1BEEE93D89420DF036C",
        ["wb:small_light|Plate|rot"] = "309F2E876C79F4313E4FC51B60F9D0C93D5F43B1C827CB2983FE5EE14A762CAC",
        ["wb:storage|Block|id"] = "0BD88B24BD66F2D4EA89148190B5E74229A043582C2A8D59A4E897A294F99AB6",
        ["wb:storage|Block|rot"] = "0762BD79FDC58DF63B470ED8A01444A6F8DCD3EC35AC5EC4B764645CB8E46612",
        ["wb:thruster|Custom|id"] = "CE1BA75AFC17F3311A0FB4DED5B717A3BE7B7E8C7E7BFF1B359918D96C1221C3",
        ["wb:thruster|Custom|rot"] = "CB1A966B61BAF2C57577CDA0053E1A03493801AD1A5FA56DCB86A01A75C5AED9",
        ["wb:truss|Block|id"] = "496A7AC96679E278C2ADFD1ABD063D847CAD72CE170305244197145F2D006859",
        ["wb:truss|Block|rot"] = "496A7AC96679E278C2ADFD1ABD063D847CAD72CE170305244197145F2D006859",
        ["wb:truss|Slab|id"] = "848FCC40E249A246309BBCB77F16D2819C6874F6B1DA70E503E312D00D7D4FE2",
        ["wb:truss|Slab|rot"] = "AA09B9366A4FC89D0FCF668A99EF3C84109A29AE7D8692A6A0D4D1D86A3F0EC1",
        ["wb:truss|Stair|id"] = "100C23FCCBC36699EC418FBDF3FFEED68EAFE6E848AF5B562753E1A9B23CADD0",
        ["wb:truss|Stair|rot"] = "D46AA3F08C67BA6CAC872467A2A0D746EE31875547964CE6C544A5B1DE7C8317",
        ["wb:truss|Slope|id"] = "4E3A54ECC478F4B9B0CC09870AF2FE265902F96B25780E642F74987788336C74",
        ["wb:truss|Slope|rot"] = "40F2DFB5A0B385B770F3F7393868E81B09641E2DD316F6705B63CECE987AAD6F",
        ["wb:truss|Column|id"] = "AB26BEF0109F2899F4EF2B3E859A475AE319E27455DA9CD44EC66D9C464AEED5",
        ["wb:truss|Column|rot"] = "2F32A0339157A56E698113B3E6FAC17D4D4810CFA06D47F782D1697E5FEA2A9F",
        ["wb:truss|Plate|id"] = "48EF2CA787EA314B3C20B4F9CDC58CF98E489204A2E22979CAF1EC1202CDCB8A",
        ["wb:truss|Plate|rot"] = "2861ABA83F17CFBB3849AAB45238491990208B1869C2439067816B5931D0C548",
        ["wb:vent|Block|id"] = "253C07A8B8CFCD575EB12B1B67DFB74E9C1094DA97A51D2F607F3997C68495A2",
        ["wb:vent|Block|rot"] = "253C07A8B8CFCD575EB12B1B67DFB74E9C1094DA97A51D2F607F3997C68495A2",
        ["wb:vent|Slab|id"] = "6E6EF714BE088E913029B3A2F83CEA7F2CBB9A14639B02EB5D9FEBD00027875D",
        ["wb:vent|Slab|rot"] = "F200E60F2FF79865F82C09B0D11657DE2503FFD2E0AE849227CD3ECEA9C4746E",
        ["wb:vent|Stair|id"] = "244E79AF110D23003A74850811345B832B39AFC6B301C7D08003E87369555C12",
        ["wb:vent|Stair|rot"] = "36E05539D4C3004936AD6650A63E1AC1FEF0BF20BC060A4581F7F6285554B429",
        ["wb:vent|Slope|id"] = "D23BFF650F07D5E3F66B02B57836FEC46C9B241724D31BCCA0956A0F60D18D13",
        ["wb:vent|Slope|rot"] = "8EBC7D2AC82E256995FE7278828A948CDFF4C94C5558D7A7FCE1BDA193CC031F",
        ["wb:vent|Column|id"] = "2821A83D5D189FD86839ED92F9C04A89686BAB68459234DC67E33E0B332A9789",
        ["wb:vent|Column|rot"] = "10AF5E4B4D3CE16A7F9F8FCA4D3574977B6E212DF75CB85F71186DB04C370401",
        ["wb:vent|Plate|id"] = "AC5ADF523FCB4F071B365185FBDA498E5F05174DD316B0AF22776B4DDDA0A90D",
        ["wb:vent|Plate|rot"] = "0F386012C2FD4E3E750FDF669476B6F6FAD51772E4761817D7E6ADDEE80068CE",
        ["__fallback__|Block|id"] = "3071583171A2A66AC59B5D22294343148A861E0E0A7A210D137457DAECF0D940",
    };

    private sealed class NaturalAlbedoLookup : ITextureLayerLookup
    {
        private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);

        public NaturalAlbedoLookup(string blockTextureDirectory)
        {
            string[] albedoNames = Directory.GetFiles(blockTextureDirectory, "*.png", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => name != null
                    && !name.EndsWith(".m.png", StringComparison.Ordinal)
                    && !name.EndsWith(".s.png", StringComparison.Ordinal)
                    && !name.EndsWith(".n.png", StringComparison.Ordinal)
                    && !name.EndsWith(".e.png", StringComparison.Ordinal)
                    && !name.EndsWith(".t.png", StringComparison.Ordinal))
                .Select(name => Path.GetFileNameWithoutExtension(name!))
                .OrderBy(name => name, new NaturalComparer())
                .ToArray();

            _indices["missing"] = 0;
            for (var i = 0; i < albedoNames.Length; i++)
            {
                _indices[albedoNames[i]] = i + 1;
            }
        }

        public int IndexOf(string name)
        {
            return _indices.TryGetValue(name, out int index) ? index : -1;
        }
    }

    private sealed class DirectoryPreviewMeshDatabase : IAssetDatabase<VoxelMesh>
    {
        private readonly Dictionary<string, VoxelMesh> _meshes = new(StringComparer.Ordinal);

        public DirectoryPreviewMeshDatabase(string directory)
        {
            var parser = new ObjParser();
            foreach (string file in Directory.GetFiles(directory, "*.obj", SearchOption.TopDirectoryOnly))
            {
                string id = Path.GetFileName(file);
                Mesh mesh = parser.Parse(new PathInfo(file));
                _meshes[id] = new VoxelMesh(mesh.Triangles, mesh.Vertices, mesh.Colors, mesh.Uv, mesh.Normals);
            }
        }

        public Result<VoxelMesh> Get(string id)
        {
            return _meshes.TryGetValue(id, out VoxelMesh mesh)
                ? Result<VoxelMesh>.FromSuccess(mesh)
                : Result<VoxelMesh>.FromFailure($"Missing mesh '{id}'.");
        }
    }

    [Fact]
    public void ShippedBricks_MatchPreExtractionMeshHashes()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var lookup = new NaturalAlbedoLookup(Path.Combine("TestFiles", "Meshing", "textures", "block"));
        var meshes = new DirectoryPreviewMeshDatabase(Path.Combine("TestFiles", "Meshing", "meshes"));
        var builder = new BrickPreviewMeshBuilder(bricks, bricks, lookup, meshes);

        List<Brick> allBricks = bricks.Get(_ => true).OrderBy(b => b.ID, StringComparer.Ordinal).ToList();
        Assert.NotEmpty(allBricks);

        var actualHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Brick brick in allBricks)
        {
            BrickShape[] shapes = brick.Shape == BrickShape.Any ? _anyShapes : [brick.Shape];
            for (var s = 0; s < shapes.Length; s++)
            {
                BrickShape shape = shapes[s];

                Result<VoxelMesh> idResult = builder.Build(brick, shape, Orientation.Identity);
                Assert.True(idResult.Success, $"Failed {brick.ID}/{shape}/Identity: {idResult.Message}");
                actualHashes[$"{brick.ID}|{shape}|id"] = HashMesh(idResult.Value);

                Result<VoxelMesh> rotResult = builder.Build(brick, shape, _rotated);
                Assert.True(rotResult.Success, $"Failed {brick.ID}/{shape}/Rotated: {rotResult.Message}");
                actualHashes[$"{brick.ID}|{shape}|rot"] = HashMesh(rotResult.Value);
            }
        }

        actualHashes["__fallback__|Block|id"] = HashMesh(builder.BuildFallback());

        Assert.Equal(_expectedHashes.Count, actualHashes.Count);
        foreach ((string key, string expectedHash) in _expectedHashes)
        {
            Assert.True(actualHashes.TryGetValue(key, out string? actualHash), $"Missing hash entry '{key}'.");
            Assert.Equal(expectedHash, actualHash);
        }
    }

    [Fact]
    public void MeshingAssembly_DoesNotReferenceEngineGraphicsOrClient()
    {
        Assembly meshingAssembly = typeof(BrickPreviewMeshBuilder).Assembly;
        AssemblyName[] references = meshingAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, r => string.Equals(r.Name, "Swordfish", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, r => string.Equals(r.Name, "WaywardBeyond.Client", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegionMeshBuilder_MeshesSingleBrickRegion()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var lookup = new NaturalAlbedoLookup(Path.Combine("TestFiles", "Meshing", "textures", "block"));
        var meshes = new DirectoryPreviewMeshDatabase(Path.Combine("TestFiles", "Meshing", "meshes"));
        var regionBuilder = new RegionMeshBuilder(bricks, lookup, meshes);

        ushort rockId = bricks.Id("wb:rock");
        Assert.NotEqual((ushort)0, rockId);

        var voxelObject = new VoxelObject(8);
        voxelObject.Set(0, 0, 0, new Voxel(rockId, new ShapeLight(BrickShape.Block, 15), 0));
        Assert.True(voxelObject.TryGetChunk(new Short3(0, 0, 0), out ChunkData chunk));
        var region = new VoxelRegionData(1, 1, 1, [new ChunkInfo(0, 0, 0, chunk.Data)], null, null);

        Result<VoxelMesh> result = regionBuilder.Build(in region);
        Assert.True(result.Success);
        Assert.Equal(24, result.Value.Vertices.Length);
        Assert.Equal(36, result.Value.Triangles.Length);
        int expectedLayer = lookup.IndexOf("rock");
        Assert.All(result.Value.Uv, uv => Assert.Equal(expectedLayer, (int)uv.Z));
    }

    private static BrickDatabase LoadShippedBricks()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
        var parseService = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);
        return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parseService, vfs, tags);
    }

    private static string HashMesh(in VoxelMesh mesh)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> buf = stackalloc byte[16];

        AppendInt(hasher, buf, mesh.Triangles.Length);
        for (var i = 0; i < mesh.Triangles.Length; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf, mesh.Triangles[i]);
            hasher.AppendData(buf[..4]);
        }

        AppendInt(hasher, buf, mesh.Vertices.Length);
        for (var i = 0; i < mesh.Vertices.Length; i++)
        {
            AppendVec3(hasher, buf, mesh.Vertices[i]);
        }

        AppendInt(hasher, buf, mesh.Colors.Length);
        for (var i = 0; i < mesh.Colors.Length; i++)
        {
            AppendVec4(hasher, buf, mesh.Colors[i]);
        }

        AppendInt(hasher, buf, mesh.Uv.Length);
        for (var i = 0; i < mesh.Uv.Length; i++)
        {
            AppendVec3(hasher, buf, mesh.Uv[i]);
        }

        AppendInt(hasher, buf, mesh.Normals.Length);
        for (var i = 0; i < mesh.Normals.Length; i++)
        {
            AppendVec3(hasher, buf, mesh.Normals[i]);
        }

        return Convert.ToHexString(hasher.GetHashAndReset());
    }

    private static void AppendInt(IncrementalHash hasher, Span<byte> buf, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        hasher.AppendData(buf[..4]);
    }

    private static void AppendVec3(IncrementalHash hasher, Span<byte> buf, Vector3 value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buf[..4], BitConverter.SingleToInt32Bits(value.X));
        BinaryPrimitives.WriteInt32LittleEndian(buf[4..8], BitConverter.SingleToInt32Bits(value.Y));
        BinaryPrimitives.WriteInt32LittleEndian(buf[8..12], BitConverter.SingleToInt32Bits(value.Z));
        hasher.AppendData(buf[..12]);
    }

    private static void AppendVec4(IncrementalHash hasher, Span<byte> buf, Vector4 value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(buf[..4], BitConverter.SingleToInt32Bits(value.X));
        BinaryPrimitives.WriteInt32LittleEndian(buf[4..8], BitConverter.SingleToInt32Bits(value.Y));
        BinaryPrimitives.WriteInt32LittleEndian(buf[8..12], BitConverter.SingleToInt32Bits(value.Z));
        BinaryPrimitives.WriteInt32LittleEndian(buf[12..16], BitConverter.SingleToInt32Bits(value.W));
        hasher.AppendData(buf);
    }
}
