// Creates or updates the mod's Steam Workshop item through the running Steam client – the same calls the game's
// Mods > Create Mod makes (CreateModMenuViewModel), without starting the game. Needs Steam running, signed in with
// the account that owns the item (or will own it); no password.
//
//   dotnet run --project tools/workshop-upload -- check  <item id>
//   dotnet run --project tools/workshop-upload -- upload <item id> <content folder> <change note file> [description file]
//   dotnet run --project tools/workshop-upload -- create <content folder> <title> <description file> <preview.jpg> <change note file>
//                                                       [--tags Scripting] [--depends 3380210757] [--visibility private|friends|unlisted|public]
//   dotnet run --project tools/workshop-upload -- visibility <item id> private|friends|unlisted|public [change note]
//   dotnet run --project tools/workshop-upload -- gallery <item id> <image file> [more image files ...]
//
// upload: title, tags, preview, visibility, dependencies and the compatibility metadata stay as they are on the item;
// with a description file (BBCode, UTF-8) the description is replaced too.
// create: makes a NEW item (private unless told otherwise, like the game's screen) and fills it like the game's
// screen does: title, description, tags, content folder, preview, the [Compatibility] line of _info.ini as metadata,
// visibility, then the dependencies. Prints the new item id – use `upload` with it for later updates.
// visibility: changes only the visibility ("public" publishes a hidden item); everything else stays as it is.
// gallery: adds the pictures to the item page's gallery, in the order given (they come after the main preview).
// Run it once: pictures already on the page are not replaced, a second run adds them again.
using System.Text;
using Steamworks;

const uint SeaPowerAppId = 1286220;
const string Usage =
    "usage:\n" +
    "  check <item id>\n" +
    "  upload <item id> <content folder> <change note file> [description file]\n" +
    "  create <content folder> <title> <description file> <preview.jpg> <change note file> [--tags a,b] [--depends id,id] [--visibility private|friends|unlisted|public]\n" +
    "  visibility <item id> private|friends|unlisted|public [change note]\n" +
    "  gallery <item id> <image file> [more image files ...]";

string command = args.Length > 0 ? args[0] : "";
ulong itemId = 0;
bool valid = command switch
{
    "check" => args.Length >= 2 && ulong.TryParse(args[1], out itemId),
    "upload" => args.Length >= 4 && ulong.TryParse(args[1], out itemId),
    "create" => args.Length >= 6,
    "visibility" => args.Length >= 3 && ulong.TryParse(args[1], out itemId) && TryVisibility(args[2], out _),
    "gallery" => args.Length >= 3 && ulong.TryParse(args[1], out itemId),
    _ => false,
};
if (!valid)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

// Outside a Steam launch the API takes the app id from the environment (or steam_appid.txt).
Environment.SetEnvironmentVariable("SteamAppId", SeaPowerAppId.ToString());
Environment.SetEnvironmentVariable("SteamGameId", SeaPowerAppId.ToString());
if (!SteamAPI.Init())
{
    Console.Error.WriteLine("SteamAPI.Init failed: is Steam running and signed in with the account that owns Sea Power?");
    return 1;
}

try
{
    return command == "create" ? Create(args) : UpdateExisting(command, itemId, args);
}
finally
{
    SteamAPI.Shutdown();
}

static int UpdateExisting(string command, ulong itemId, string[] args)
{
    var item = new PublishedFileId_t(itemId);
    if (!Details(item, out SteamUGCDetails_t details))
        return 1;
    Console.WriteLine($"Item {itemId}: \"{details.m_rgchTitle}\", owner {details.m_ulSteamIDOwner}, " +
                      $"visibility {details.m_eVisibility}, updated {DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeUpdated):u}");
    ulong me = SteamUser.GetSteamID().m_SteamID;
    if (details.m_ulSteamIDOwner != me)
    {
        Console.Error.WriteLine($"Signed in as {me}, not the owner of this item - not uploading.");
        return 1;
    }
    if (command == "check")
    {
        Verify(item);
        return 0;
    }
    if (command == "visibility")
        return SetVisibility(item, args);
    if (command == "gallery")
        return AddGallery(item, args);

    string folder = Path.GetFullPath(args[2]);
    string note = File.ReadAllText(args[3]).Trim();
    if (!File.Exists(Path.Combine(folder, "_info.ini")))
    {
        Console.Error.WriteLine($"{folder} has no _info.ini - not a mod folder.");
        return 1;
    }

    UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(SeaPowerAppId), item);
    if (!SteamUGC.SetItemContent(update, folder.Replace("\\", "/")))
    {
        Console.Error.WriteLine("SetItemContent refused the folder.");
        return 1;
    }
    if (args.Length > 4)
    {
        string description = File.ReadAllText(args[4], Encoding.UTF8);
        SteamUGC.SetItemDescription(update, description);
        Console.WriteLine($"New description: {description.Length} characters.");
    }
    int code = Submit(update, note, folder);
    if (Details(item, out details))
        Console.WriteLine($"Item now updated {DateTimeOffset.FromUnixTimeSeconds(details.m_rtimeUpdated):u}");
    return code;
}

static int Create(string[] args)
{
    string folder = Path.GetFullPath(args[1]);
    string title = args[2];
    string description = File.ReadAllText(args[3], Encoding.UTF8);
    string preview = Path.GetFullPath(args[4]);
    string note = File.ReadAllText(args[5]).Trim();
    var tags = new List<string>();
    var depends = new List<ulong>();
    var visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
    for (int i = 6; i + 1 < args.Length; i += 2)
    {
        string value = args[i + 1];
        switch (args[i])
        {
            case "--tags":
                tags.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                break;
            case "--depends":
                foreach (string d in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!ulong.TryParse(d, out ulong id) || id == 0)
                    {
                        Console.Error.WriteLine($"'{d}' is not a Workshop item id.");
                        return 2;
                    }
                    depends.Add(id);
                }
                break;
            case "--visibility":
                if (!TryVisibility(value, out visibility))
                {
                    Console.Error.WriteLine($"Unknown visibility '{value}'.");
                    return 2;
                }
                break;
            default:
                Console.Error.WriteLine($"Unknown option '{args[i]}'.\n{Usage}");
                return 2;
        }
    }
    if (!File.Exists(Path.Combine(folder, "_info.ini")))
    {
        Console.Error.WriteLine($"{folder} has no _info.ini - not a mod folder.");
        return 1;
    }
    if (!File.Exists(preview) || new FileInfo(preview).Length > 1_000_000)
    {
        Console.Error.WriteLine($"{preview}: missing, or over 1 MB (Steam's limit for a preview image).");
        return 1;
    }

    Console.WriteLine($"Creating a new Workshop item \"{title}\" ({visibility}, tags: {string.Join(", ", tags)}, depends on: {string.Join(", ", depends)}) ...");
    CreateItemResult_t created = default;
    bool done = false, ioFailure = false;
    var createCall = CallResult<CreateItemResult_t>.Create((r, failed) => { created = r; ioFailure = failed; done = true; });
    createCall.Set(SteamUGC.CreateItem(new AppId_t(SeaPowerAppId), EWorkshopFileType.k_EWorkshopFileTypeFirst));
    if (!Pump(() => done, TimeSpan.FromSeconds(60)) || ioFailure || created.m_eResult != EResult.k_EResultOK)
    {
        Console.Error.WriteLine($"CreateItem failed: {(done ? created.m_eResult.ToString() : "timeout")}{(ioFailure ? " (I/O failure)" : "")}");
        return 1;
    }
    if (created.m_bUserNeedsToAcceptWorkshopLegalAgreement)
    {
        Console.Error.WriteLine("Steam wants the Workshop legal agreement accepted first: https://steamcommunity.com/sharedfiles/workshoplegalagreement " +
                                $"(an empty private item {created.m_nPublishedFileId.m_PublishedFileId} was created; use `upload` with it afterwards).");
        return 1;
    }
    var item = created.m_nPublishedFileId;
    Console.WriteLine($"Created item {item.m_PublishedFileId}.");

    UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(SeaPowerAppId), item);
    SteamUGC.SetItemTitle(update, title);
    SteamUGC.SetItemDescription(update, description);
    if (tags.Count > 0)
        SteamUGC.SetItemTags(update, tags);
    if (!SteamUGC.SetItemContent(update, folder.Replace("\\", "/")))
    {
        Console.Error.WriteLine($"SetItemContent refused the folder. The empty private item {item.m_PublishedFileId} exists - fix it and run `upload`.");
        return 1;
    }
    SteamUGC.SetItemPreview(update, preview.Replace("\\", "/"));
    string metadata = CompatibilityMetadata(Path.Combine(folder, "_info.ini"));
    if (!string.IsNullOrEmpty(metadata))
    {
        SteamUGC.SetItemMetadata(update, metadata);
        Console.WriteLine($"Compatibility metadata: {metadata}");
    }
    SteamUGC.SetItemVisibility(update, visibility);
    foreach (ulong dependency in depends)
    {
        if (dependency == item.m_PublishedFileId)
            continue;
        AddUGCDependencyResult_t added = default;
        bool addDone = false;
        var addCall = CallResult<AddUGCDependencyResult_t>.Create((r, failed) => { added = r; addDone = true; });
        addCall.Set(SteamUGC.AddDependency(item, new PublishedFileId_t(dependency)));
        Pump(() => addDone, TimeSpan.FromSeconds(30));
        Console.WriteLine($"Dependency {dependency}: {(addDone ? added.m_eResult.ToString() : "timeout")}");
    }
    int code = Submit(update, note, folder);
    Console.WriteLine($"Item: https://steamcommunity.com/sharedfiles/filedetails/?id={item.m_PublishedFileId}");
    return code;
}

// The game writes the [Compatibility] lines of _info.ini into the item's metadata, joined by ';' (ModCompatibility).
static string CompatibilityMetadata(string iniPath)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    bool inSection = false;
    foreach (string raw in File.ReadAllLines(iniPath))
    {
        string line = raw.Trim();
        if (line.StartsWith("["))
        {
            inSection = line.Equals("[Compatibility]", StringComparison.OrdinalIgnoreCase);
            continue;
        }
        int eq = line.IndexOf('=');
        if (inSection && eq > 0)
            values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
    }
    var parts = new List<string>();
    foreach (string key in new[] { "ExactVersion", "ApproximateVersion", "GreaterThanVersion", "GreaterThanEqualToVersion", "LessThanVersion", "LessThanEqualToVersion" })
    {
        if (values.TryGetValue(key, out string v) && v.Length > 0)
            parts.Add($"{key}={v}");
    }
    if (values.TryGetValue("ExcludeVersions", out string exclude) && exclude.Length > 0)
        parts.Add("ExcludeVersions=" + string.Join(",", exclude.Split(',').Select(x => x.Trim())));
    return string.Join(";", parts);
}

static int Submit(UGCUpdateHandle_t update, string note, string folder)
{
    SubmitItemUpdateResult_t result = default;
    bool done = false, ioFailure = false;
    var call = CallResult<SubmitItemUpdateResult_t>.Create((r, failed) => { result = r; ioFailure = failed; done = true; });
    call.Set(SteamUGC.SubmitItemUpdate(update, note));
    Console.WriteLine($"Uploading {folder} ...");
    DateTime nextReport = DateTime.MinValue;
    while (!done)
    {
        SteamAPI.RunCallbacks();
        if (DateTime.Now >= nextReport)
        {
            EItemUpdateStatus status = SteamUGC.GetItemUpdateProgress(update, out ulong processed, out ulong total);
            Console.WriteLine($"  {status} {processed / 1048576.0:0.0} / {total / 1048576.0:0.0} MB");
            nextReport = DateTime.Now.AddSeconds(5);
        }
        Thread.Sleep(100);
    }
    Console.WriteLine($"Result: {result.m_eResult}{(ioFailure ? " (I/O failure)" : "")}");
    if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
        Console.WriteLine("Steam wants the Workshop legal agreement accepted on the item page before it goes live.");
    return result.m_eResult == EResult.k_EResultOK && !ioFailure ? 0 : 1;
}

static bool Pump(Func<bool> done, TimeSpan timeout)
{
    DateTime until = DateTime.Now + timeout;
    while (!done() && DateTime.Now < until)
    {
        SteamAPI.RunCallbacks();
        Thread.Sleep(100);
    }
    return done();
}

static bool TryVisibility(string value, out ERemoteStoragePublishedFileVisibility visibility)
{
    visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
    switch (value.ToLowerInvariant())
    {
        case "private": return true;
        case "friends": visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly; return true;
        case "unlisted": visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted; return true;
        case "public": visibility = ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic; return true;
        default: return false;
    }
}

static int SetVisibility(PublishedFileId_t item, string[] args)
{
    TryVisibility(args[2], out ERemoteStoragePublishedFileVisibility visibility);
    string note = args.Length > 3 ? args[3] : "Visibility changed.";
    UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(SeaPowerAppId), item);
    if (!SteamUGC.SetItemVisibility(update, visibility))
    {
        Console.Error.WriteLine("SetItemVisibility refused the value.");
        return 1;
    }
    Console.WriteLine($"Setting the visibility to {visibility} ...");
    int code = Submit(update, note, "visibility change");
    if (Details(item, out SteamUGCDetails_t after))
        Console.WriteLine($"Visibility now: {after.m_eVisibility}");
    return code;
}

static int AddGallery(PublishedFileId_t item, string[] args)
{
    UGCUpdateHandle_t update = SteamUGC.StartItemUpdate(new AppId_t(SeaPowerAppId), item);
    for (int i = 2; i < args.Length; i++)
    {
        string file = Path.GetFullPath(args[i]);
        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"{file}: not found - nothing was added.");
            return 1;
        }
        if (!SteamUGC.AddItemPreviewFile(update, file.Replace("\\", "/"), EItemPreviewType.k_EItemPreviewType_Image))
        {
            Console.Error.WriteLine($"Steam refused {file} - nothing was added.");
            return 1;
        }
        Console.WriteLine($"  + {Path.GetFileName(file)}");
    }
    return Submit(update, "Screenshots added.", $"{args.Length - 2} pictures");
}

// What the item page will show: tags, size, description length, preview, the compatibility metadata and the required
// items – to check a freshly created item without opening the web page.
static void Verify(PublishedFileId_t item)
{
    UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { item }, 1);
    SteamUGC.SetReturnLongDescription(query, true);
    SteamUGC.SetReturnMetadata(query, true);
    SteamUGC.SetReturnChildren(query, true);
    SteamUGC.SetReturnAdditionalPreviews(query, true);
    SteamUGCQueryCompleted_t completed = default;
    bool done = false, failed = false;
    var call = CallResult<SteamUGCQueryCompleted_t>.Create((r, f) => { completed = r; failed = f; done = true; });
    call.Set(SteamUGC.SendQueryUGCRequest(query));
    Pump(() => done, TimeSpan.FromSeconds(30));
    if (!done || failed || completed.m_eResult != EResult.k_EResultOK || completed.m_unNumResultsReturned == 0
        || !SteamUGC.GetQueryUGCResult(query, 0, out SteamUGCDetails_t d))
    {
        Console.Error.WriteLine("Could not read the item for verification.");
        SteamUGC.ReleaseQueryUGCRequest(query);
        return;
    }
    Console.WriteLine($"  title:       {d.m_rgchTitle}");
    Console.WriteLine($"  visibility:  {d.m_eVisibility}");
    Console.WriteLine($"  tags:        {d.m_rgchTags}");
    Console.WriteLine($"  file size:   {d.m_nFileSize / 1024.0:0} KB");
    Console.WriteLine($"  description: {d.m_rgchDescription.Length} characters, starts \"{d.m_rgchDescription.Substring(0, Math.Min(60, d.m_rgchDescription.Length)).Replace("\n", " ")}\"");
    if (SteamUGC.GetQueryUGCPreviewURL(query, 0, out string preview, 512))
        Console.WriteLine($"  preview:     {(string.IsNullOrEmpty(preview) ? "(none)" : preview)}");
    if (SteamUGC.GetQueryUGCMetadata(query, 0, out string metadata, 5000))
        Console.WriteLine($"  metadata:    {metadata}");
    uint extra = SteamUGC.GetQueryUGCNumAdditionalPreviews(query, 0);
    Console.WriteLine($"  gallery:     {extra} more picture(s) after the main preview");
    for (uint i = 0; i < extra; i++)
    {
        if (SteamUGC.GetQueryUGCAdditionalPreview(query, 0, i, out string url, 512, out string name, 256, out EItemPreviewType type))
            Console.WriteLine($"    {i + 1,2}. {type} {name}");
    }
    var children = new PublishedFileId_t[Math.Max(1, (int)d.m_unNumChildren)];
    if (d.m_unNumChildren > 0 && SteamUGC.GetQueryUGCChildren(query, 0, children, d.m_unNumChildren))
        Console.WriteLine($"  requires:    {string.Join(", ", children.Take((int)d.m_unNumChildren).Select(c => c.m_PublishedFileId))}");
    else
        Console.WriteLine("  requires:    (none)");
    SteamUGC.ReleaseQueryUGCRequest(query);
}

static bool Details(PublishedFileId_t item, out SteamUGCDetails_t details)
{
    details = default;
    UGCQueryHandle_t query = SteamUGC.CreateQueryUGCDetailsRequest(new[] { item }, 1);
    SteamUGCQueryCompleted_t completed = default;
    bool done = false, failed = false;
    var call = CallResult<SteamUGCQueryCompleted_t>.Create((r, f) => { completed = r; failed = f; done = true; });
    call.Set(SteamUGC.SendQueryUGCRequest(query));
    Pump(() => done, TimeSpan.FromSeconds(30));
    bool ok = done && !failed && completed.m_eResult == EResult.k_EResultOK && completed.m_unNumResultsReturned > 0
              && SteamUGC.GetQueryUGCResult(query, 0, out details) && details.m_eResult == EResult.k_EResultOK;
    SteamUGC.ReleaseQueryUGCRequest(query);
    if (!ok)
        Console.Error.WriteLine($"Could not read item {item.m_PublishedFileId} ({(done ? completed.m_eResult.ToString() : "timeout")}).");
    return ok;
}
