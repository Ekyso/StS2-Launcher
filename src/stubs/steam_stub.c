// Stub Steamworks SDK for Android. Satisfies Steamworks.NET P/Invoke symbols
// with no-op implementations that return safe defaults.

#include <stdint.h>
#include <string.h>

static int dummy_steam_object = 0;

int SteamAPI_RestartAppIfNecessary(uint32_t unOwnAppID) {
    return 0;
}

int SteamInternal_SteamAPI_Init(const char *pszInternalCheckInterfaceVersions,
                                 char *pOutErrMsg) {
    if (pOutErrMsg) {
        memset(pOutErrMsg, 0, 1024);
    }
    return 0;
}

int SteamAPI_Init(void) { return 1; }
int SteamAPI_InitSafe(void) { return 1; }

int SteamAPI_InitFlat(char *pOutErrMsg) {
    if (pOutErrMsg) {
        memset(pOutErrMsg, 0, 1024);
    }
    return 0;
}

int SteamAPI_InitAnonymousUser(void) { return 1; }
void SteamAPI_Shutdown(void) {}
void SteamAPI_RunCallbacks(void) {}
void SteamAPI_ReleaseCurrentThreadMemory(void) {}
void SteamAPI_SetTryCatchCallbacks(int bTryCatchCallbacks) {}
void SteamAPI_WriteMiniDump(uint32_t uStructuredExceptionCode, void *pvExceptionInfo, uint32_t uBuildID) {}
void SteamAPI_SetMiniDumpComment(const char *pchMsg) {}

int SteamAPI_IsSteamRunning(void) { return 0; }
int32_t SteamAPI_GetSteamInstallPath(void) { return 0; }
int32_t SteamAPI_GetHSteamPipe(void) { return 1; }
int32_t SteamAPI_GetHSteamUser(void) { return 1; }
void *SteamClient(void) { return &dummy_steam_object; }

void *SteamAPI_ISteamClient_GetISteamUser(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamGameServer(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamFriends(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamUtils(void *self, int32_t hSteamPipe,
    const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamMatchmaking(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamMatchmakingServers(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamGenericInterface(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamUserStats(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamGameServerStats(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamApps(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamNetworking(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamRemoteStorage(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamScreenshots(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamGameSearch(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamHTTP(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamController(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamUGC(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamMusic(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamMusicRemote(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamHTMLSurface(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamInventory(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamVideo(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamParentalSettings(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamInput(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamParties(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamRemotePlay(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamNetworkingSockets(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamNetworkingUtils(void *self, int32_t hSteamPipe,
    const char *pchVersion) { return &dummy_steam_object; }
void *SteamAPI_ISteamClient_GetISteamNetworkingMessages(void *self, int32_t hSteamUser,
    int32_t hSteamPipe, const char *pchVersion) { return &dummy_steam_object; }

int32_t SteamAPI_ISteamClient_CreateSteamPipe(void *self) { return 1; }
int SteamAPI_ISteamClient_BReleaseSteamPipe(void *self, int32_t hSteamPipe) { return 1; }
int32_t SteamAPI_ISteamClient_ConnectToGlobalUser(void *self, int32_t hSteamPipe) { return 1; }
int32_t SteamAPI_ISteamClient_CreateLocalUser(void *self, int32_t *phSteamPipe, int eAccountType) { return 1; }
void SteamAPI_ISteamClient_ReleaseUser(void *self, int32_t hSteamPipe, int32_t hUser) {}
void SteamAPI_ISteamClient_SetLocalIPBinding(void *self, void *unIP, uint16_t usPort) {}
uint32_t SteamAPI_ISteamClient_GetIPCCallCount(void *self) { return 0; }
void SteamAPI_ISteamClient_SetWarningMessageHook(void *self, void *pFunction) {}
int SteamAPI_ISteamClient_BShutdownIfAllPipesClosed(void *self) { return 0; }
void SteamAPI_ManualDispatch_Init(void) {}
void SteamAPI_ManualDispatch_RunFrame(int32_t hSteamPipe) {}
int SteamAPI_ManualDispatch_GetNextCallback(int32_t hSteamPipe, void *pCallbackMsg) { return 0; }
void SteamAPI_ManualDispatch_FreeLastCallback(int32_t hSteamPipe) {}
int SteamAPI_ManualDispatch_GetAPICallResult(int32_t hSteamPipe, uint64_t hSteamAPICall,
    void *pCallback, int cubCallback, int iCallbackExpected, int *pbFailed) { return 0; }

void SteamAPI_RegisterCallback(void *pCallback, int iCallback) {}
void SteamAPI_UnregisterCallback(void *pCallback) {}
void SteamAPI_RegisterCallResult(void *pCallback, uint64_t hAPICall) {}
void SteamAPI_UnregisterCallResult(void *pCallback, uint64_t hAPICall) {}
void SteamAPI_CheckCallbackRegistered_t(void) {}
void *SteamInternal_FindOrCreateUserInterface(int32_t hSteamUser, const char *pszVersion) { return &dummy_steam_object; }
void *SteamInternal_FindOrCreateGameServerInterface(int32_t hSteamUser, const char *pszVersion) { return &dummy_steam_object; }
void *SteamInternal_CreateInterface(const char *pszVersion) { return &dummy_steam_object; }
void *SteamInternal_ContextInit(void *pContextInitData) { return &dummy_steam_object; }
uint64_t SteamAPI_ISteamUser_GetSteamID(void *self) { return 76561198000000000ULL; }
int SteamAPI_ISteamUser_BLoggedOn(void *self) { return 1; }
int SteamAPI_ISteamUser_GetAuthSessionTicket(void *self, void *pTicket, int cbMaxTicket, uint32_t *pcbTicket, void *pSteamNetworkingIdentity) { return 0; }
void SteamAPI_ISteamUser_CancelAuthTicket(void *self, int hAuthTicket) {}
int SteamAPI_ISteamUser_UserHasLicenseForApp(void *self, uint64_t steamID, uint32_t appID) { return 0; }
int SteamAPI_ISteamApps_BIsSubscribed(void *self) { return 1; }
int SteamAPI_ISteamApps_BIsSubscribedApp(void *self, uint32_t appID) { return 1; }
int SteamAPI_ISteamApps_BIsDlcInstalled(void *self, uint32_t appID) { return 1; }
const char *SteamAPI_ISteamApps_GetCurrentGameLanguage(void *self) { return "english"; }
const char *SteamAPI_ISteamApps_GetAvailableGameLanguages(void *self) { return "english"; }
int SteamAPI_ISteamApps_GetDLCCount(void *self) { return 0; }
int SteamAPI_ISteamApps_BGetDLCDataByIndex(void *self, int iDLC, uint32_t *pAppID, int *pbAvailable, char *pchName, int cchNameBufferSize) { return 0; }
int SteamAPI_ISteamApps_GetAppBuildId(void *self) { return 1; }
uint64_t SteamAPI_ISteamApps_GetAppOwner(void *self) { return 76561198000000000ULL; }
int SteamAPI_ISteamApps_BIsAppInstalled(void *self, uint32_t appID) { return 1; }
int SteamAPI_ISteamApps_BIsLowViolence(void *self) { return 0; }
int SteamAPI_ISteamApps_BIsCybercafe(void *self) { return 0; }
int SteamAPI_ISteamApps_BIsVACBanned(void *self) { return 0; }
int SteamAPI_ISteamApps_BIsSubscribedFromFreeWeekend(void *self) { return 0; }
int SteamAPI_ISteamApps_BIsSubscribedFromFamilySharing(void *self) { return 0; }
int SteamAPI_ISteamApps_BIsTimedTrial(void *self, uint32_t *punSecondsAllowed, uint32_t *punSecondsPlayed) { return 0; }
uint32_t SteamAPI_ISteamApps_GetEarliestPurchaseUnixTime(void *self, uint32_t nAppID) { return 0; }
int SteamAPI_ISteamApps_GetCurrentBetaName(void *self, char *pchName, int cchNameBufferSize) {
    if (pchName && cchNameBufferSize > 0) pchName[0] = 0;
    return 0;
}
int SteamAPI_ISteamApps_GetNumBetas(void *self) { return 0; }
int SteamAPI_ISteamApps_GetBetaInfo(void *self, int iBetaIndex, uint32_t *punFlags, uint32_t *punBuildID, char *pchBetaName, int cchBetaName, char *pchDescription, int cchDescription) { return 0; }
int SteamAPI_ISteamApps_SetActiveBeta(void *self, const char *pchBetaName) { return 0; }
uint32_t SteamAPI_ISteamApps_GetInstalledDepots(void *self, uint32_t appID, uint32_t *pvecDepots, uint32_t cMaxDepots) { return 0; }
uint32_t SteamAPI_ISteamApps_GetAppInstallDir(void *self, uint32_t appID, char *pchFolder, uint32_t cchFolderBufferSize) {
    if (pchFolder && cchFolderBufferSize > 0) pchFolder[0] = 0;
    return 0;
}
int SteamAPI_ISteamApps_GetDlcDownloadProgress(void *self, uint32_t nAppID, uint64_t *punBytesDownloaded, uint64_t *punBytesTotal) { return 0; }
void SteamAPI_ISteamApps_InstallDLC(void *self, uint32_t nAppID) {}
void SteamAPI_ISteamApps_UninstallDLC(void *self, uint32_t nAppID) {}
void SteamAPI_ISteamApps_RequestAppProofOfPurchaseKey(void *self, uint32_t nAppID) {}
void SteamAPI_ISteamApps_RequestAllProofOfPurchaseKeys(void *self) {}
uint32_t SteamAPI_ISteamApps_GetLaunchCommandLine(void *self, char *pszCommandLine, int cubCommandLine) {
    if (pszCommandLine && cubCommandLine > 0) pszCommandLine[0] = 0;
    return 0;
}
const char *SteamAPI_ISteamApps_GetLaunchQueryParam(void *self, const char *pchKey) { return ""; }
uint64_t SteamAPI_ISteamApps_GetFileDetails(void *self, const char *pszFileName) { return 0; }
int SteamAPI_ISteamApps_MarkContentCorrupt(void *self, int bMissingFilesOnly) { return 0; }
void SteamAPI_ISteamApps_SetDlcContext(void *self, uint32_t nAppID) {}
uint32_t SteamAPI_ISteamUtils_GetSecondsSinceAppActive(void *self) { return 0; }
uint32_t SteamAPI_ISteamUtils_GetSecondsSinceComputerActive(void *self) { return 0; }
uint32_t SteamAPI_ISteamUtils_GetServerRealTime(void *self) { return 0; }
const char *SteamAPI_ISteamUtils_GetIPCountry(void *self) { return "US"; }
uint32_t SteamAPI_ISteamUtils_GetAppID(void *self) { return 2868840; }
int SteamAPI_ISteamUtils_IsOverlayEnabled(void *self) { return 0; }
int SteamAPI_ISteamUtils_IsSteamInBigPictureMode(void *self) { return 0; }
int SteamAPI_ISteamUtils_IsSteamRunningOnSteamDeck(void *self) { return 0; }
int SteamAPI_ISteamUtils_IsVRHeadsetStreamingEnabled(void *self) { return 0; }
int SteamAPI_ISteamUtils_IsSteamRunningInVR(void *self) { return 0; }
const char *SteamAPI_ISteamUtils_GetSteamUILanguage(void *self) { return "english"; }
int SteamAPI_ISteamUtils_GetCurrentBatteryPower(void *self) { return 255; }
int SteamAPI_ISteamUtils_GetImageSize(void *self, int iImage, uint32_t *pnWidth, uint32_t *pnHeight) { return 0; }
int SteamAPI_ISteamUtils_GetImageRGBA(void *self, int iImage, uint8_t *pubDest, int nDestBufferSize) { return 0; }
int SteamAPI_ISteamUtils_GetEnteredGamepadTextLength(void *self) { return 0; }
int SteamAPI_ISteamUtils_GetEnteredGamepadTextInput(void *self, char *pchText, uint32_t cchText) { return 0; }
int SteamAPI_ISteamUtils_ShowGamepadTextInput(void *self, int eInputMode, int eLineInputMode, const char *pchDescription, uint32_t unCharMax, const char *pchExistingText) { return 0; }
int SteamAPI_ISteamUtils_ShowFloatingGamepadTextInput(void *self, int eKeyboardMode, int nTextFieldXPosition, int nTextFieldYPosition, int nTextFieldWidth, int nTextFieldHeight) { return 0; }
int SteamAPI_ISteamUtils_DismissFloatingGamepadTextInput(void *self) { return 0; }
int SteamAPI_ISteamUtils_DismissGamepadTextInput(void *self) { return 0; }
void SteamAPI_ISteamUtils_SetOverlayNotificationPosition(void *self, int eNotificationPosition) {}
void SteamAPI_ISteamUtils_SetOverlayNotificationInset(void *self, int nHorizontalInset, int nVerticalInset) {}
int SteamAPI_ISteamUtils_SetWarningMessageHook(void *self, void *pFunction) { return 0; }
int SteamAPI_ISteamUtils_BOverlayNeedsPresent(void *self) { return 0; }
uint64_t SteamAPI_ISteamUtils_CheckFileSignature(void *self, const char *szFileName) { return 0; }
int SteamAPI_ISteamUtils_InitFilterText(void *self, uint32_t unFilterOptions) { return 0; }
int SteamAPI_ISteamUtils_FilterText(void *self, int eContext, uint64_t sourceSteamID, const char *pchInputMessage, char *pchOutFilteredText, uint32_t nByteSizeOutFilteredText) { return 0; }
const char *SteamAPI_ISteamFriends_GetPersonaName(void *self) { return "Player"; }
int SteamAPI_ISteamFriends_GetPersonaState(void *self) { return 1; }
int SteamAPI_ISteamFriends_GetFriendCount(void *self, int iFriendFlags) { return 0; }
uint64_t SteamAPI_ISteamFriends_GetFriendByIndex(void *self, int iFriend, int iFriendFlags) { return 0; }
const char *SteamAPI_ISteamFriends_GetFriendPersonaName(void *self, uint64_t steamIDFriend) { return ""; }
int SteamAPI_ISteamFriends_GetFriendPersonaState(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_GetFriendRelationship(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_HasFriend(void *self, uint64_t steamIDFriend, int iFriendFlags) { return 0; }
void SteamAPI_ISteamFriends_ActivateGameOverlay(void *self, const char *pchDialog) {}
void SteamAPI_ISteamFriends_ActivateGameOverlayToWebPage(void *self, const char *pchURL, int eMode) {}
void SteamAPI_ISteamFriends_ActivateGameOverlayToUser(void *self, const char *pchDialog, uint64_t steamID) {}
void SteamAPI_ISteamFriends_ActivateGameOverlayToStore(void *self, uint32_t nAppID, int eFlag) {}
void SteamAPI_ISteamFriends_ActivateGameOverlayInviteDialog(void *self, uint64_t steamIDLobby) {}
void SteamAPI_ISteamFriends_ActivateGameOverlayInviteDialogConnectString(void *self, const char *pchConnectString) {}
void SteamAPI_ISteamFriends_SetRichPresence(void *self, const char *pchKey, const char *pchValue) {}
void SteamAPI_ISteamFriends_ClearRichPresence(void *self) {}
const char *SteamAPI_ISteamFriends_GetFriendRichPresence(void *self, uint64_t steamIDFriend, const char *pchKey) { return ""; }
int SteamAPI_ISteamFriends_GetFriendRichPresenceKeyCount(void *self, uint64_t steamIDFriend) { return 0; }
const char *SteamAPI_ISteamFriends_GetFriendRichPresenceKeyByIndex(void *self, uint64_t steamIDFriend, int iKey) { return ""; }
int SteamAPI_ISteamFriends_GetSmallFriendAvatar(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_GetMediumFriendAvatar(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_GetLargeFriendAvatar(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_GetFriendGamePlayed(void *self, uint64_t steamIDFriend, void *pFriendGameInfo) { return 0; }
void SteamAPI_ISteamFriends_RequestFriendRichPresence(void *self, uint64_t steamIDFriend) {}
uint64_t SteamAPI_ISteamFriends_RequestUserInformation(void *self, uint64_t steamIDUser, int bRequireNameOnly) { return 0; }
const char *SteamAPI_ISteamFriends_GetPlayerNickname(void *self, uint64_t steamIDPlayer) { return NULL; }
int SteamAPI_ISteamFriends_GetFriendSteamLevel(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_GetUserRestrictions(void *self) { return 0; }
int SteamAPI_ISteamFriends_GetCoplayFriendCount(void *self) { return 0; }
uint64_t SteamAPI_ISteamFriends_GetCoplayFriend(void *self, int iCoplayFriend) { return 0; }
int SteamAPI_ISteamFriends_GetFriendCoplayTime(void *self, uint64_t steamIDFriend) { return 0; }
uint32_t SteamAPI_ISteamFriends_GetFriendCoplayGame(void *self, uint64_t steamIDFriend) { return 0; }
int SteamAPI_ISteamFriends_InviteUserToGame(void *self, uint64_t steamIDFriend, const char *pchConnectString) { return 0; }
int SteamAPI_ISteamFriends_SetListenForFriendsMessages(void *self, int bInterceptEnabled) { return 0; }
int SteamAPI_ISteamFriends_ReplyToFriendMessage(void *self, uint64_t steamIDFriend, const char *pchMsgToSend) { return 0; }
int SteamAPI_ISteamFriends_GetFriendMessage(void *self, uint64_t steamIDFriend, int iMessageID, void *pvData, int cubData, int *peChatEntryType) { return 0; }
int SteamAPI_ISteamFriends_GetClanCount(void *self) { return 0; }
uint64_t SteamAPI_ISteamFriends_GetClanByIndex(void *self, int iClan) { return 0; }
const char *SteamAPI_ISteamFriends_GetClanName(void *self, uint64_t steamIDClan) { return ""; }
const char *SteamAPI_ISteamFriends_GetClanTag(void *self, uint64_t steamIDClan) { return ""; }
int SteamAPI_ISteamFriends_GetClanActivityCounts(void *self, uint64_t steamIDClan, int *pnOnline, int *pnInGame, int *pnChatting) { return 0; }
int SteamAPI_ISteamFriends_GetClanOfficerCount(void *self, uint64_t steamIDClan) { return 0; }
uint64_t SteamAPI_ISteamFriends_GetClanOwner(void *self, uint64_t steamIDClan) { return 0; }
uint64_t SteamAPI_ISteamFriends_GetClanOfficerByIndex(void *self, uint64_t steamIDClan, int iOfficer) { return 0; }
int SteamAPI_ISteamFriends_GetFriendsGroupCount(void *self) { return 0; }
int16_t SteamAPI_ISteamFriends_GetFriendsGroupIDByIndex(void *self, int iFG) { return 0; }
const char *SteamAPI_ISteamFriends_GetFriendsGroupName(void *self, int16_t friendsGroupID) { return ""; }
int SteamAPI_ISteamFriends_GetFriendsGroupMembersCount(void *self, int16_t friendsGroupID) { return 0; }
void SteamAPI_ISteamFriends_GetFriendsGroupMembersList(void *self, int16_t friendsGroupID, uint64_t *pOutSteamIDMembers, int nMembersCount) {}
uint64_t SteamAPI_ISteamFriends_GetFollowerCount(void *self, uint64_t steamID) { return 0; }
uint64_t SteamAPI_ISteamFriends_IsFollowing(void *self, uint64_t steamID) { return 0; }
uint64_t SteamAPI_ISteamFriends_EnumerateFollowingList(void *self, uint32_t unStartIndex) { return 0; }
int SteamAPI_ISteamFriends_IsClanPublic(void *self, uint64_t steamIDClan) { return 0; }
int SteamAPI_ISteamFriends_IsClanOfficialGameGroup(void *self, uint64_t steamIDClan) { return 0; }
int SteamAPI_ISteamFriends_RegisterProtocolInOverlayBrowser(void *self, const char *pchProtocol) { return 0; }
uint64_t SteamAPI_ISteamFriends_DownloadClanActivityCounts(void *self, uint64_t *psteamIDClans, int cClansToRequest) { return 0; }
uint64_t SteamAPI_ISteamFriends_JoinClanChatRoom(void *self, uint64_t steamIDClan) { return 0; }
int SteamAPI_ISteamFriends_LeaveClanChatRoom(void *self, uint64_t steamIDClan) { return 0; }
int SteamAPI_ISteamFriends_GetClanChatMemberCount(void *self, uint64_t steamIDClan) { return 0; }
uint64_t SteamAPI_ISteamFriends_GetChatMemberByIndex(void *self, uint64_t steamIDClan, int iUser) { return 0; }
int SteamAPI_ISteamFriends_SendClanChatMessage(void *self, uint64_t steamIDClanChat, const char *pchText) { return 0; }
int SteamAPI_ISteamFriends_GetClanChatMessage(void *self, uint64_t steamIDClanChat, int iMessage, void *prgchText, int cchTextMax, int *peChatEntryType, uint64_t *psteamidChatter) { return 0; }
int SteamAPI_ISteamFriends_IsClanChatAdmin(void *self, uint64_t steamIDClanChat, uint64_t steamIDUser) { return 0; }
int SteamAPI_ISteamFriends_IsClanChatWindowOpenInSteam(void *self, uint64_t steamIDClanChat) { return 0; }
int SteamAPI_ISteamFriends_OpenClanChatWindowInSteam(void *self, uint64_t steamIDClanChat) { return 0; }
int SteamAPI_ISteamFriends_CloseClanChatWindowInSteam(void *self, uint64_t steamIDClanChat) { return 0; }
uint64_t SteamAPI_ISteamFriends_IsUserInSource(void *self, uint64_t steamIDUser, uint64_t steamIDSource) { return 0; }
uint64_t SteamAPI_ISteamFriends_SetPersonaName(void *self, const char *pchPersonaName) { return 0; }
uint64_t SteamAPI_ISteamFriends_RequestClanOfficerList(void *self, uint64_t steamIDClan) { return 0; }
int SteamAPI_ISteamFriends_GetNumChatsWithUnreadPriorityMessages(void *self) { return 0; }
void SteamAPI_ISteamFriends_SetPlayedWith(void *self, uint64_t steamIDUserPlayedWith) {}
int SteamAPI_ISteamFriends_BHasEquippedProfileItem(void *self, uint64_t steamID, int itemType) { return 0; }
uint64_t SteamAPI_ISteamFriends_RequestEquippedProfileItems(void *self, uint64_t steamID) { return 0; }
int SteamAPI_ISteamUserStats_RequestCurrentStats(void *self) { return 1; }
int SteamAPI_ISteamUserStats_GetStatInt32(void *self, const char *pchName, int32_t *pData) {
    if (pData) *pData = 0;
    return 1;
}
int SteamAPI_ISteamUserStats_GetStatFloat(void *self, const char *pchName, float *pData) {
    if (pData) *pData = 0.0f;
    return 1;
}
int SteamAPI_ISteamUserStats_SetStatInt32(void *self, const char *pchName, int32_t nData) { return 1; }
int SteamAPI_ISteamUserStats_SetStatFloat(void *self, const char *pchName, float fData) { return 1; }
int SteamAPI_ISteamUserStats_UpdateAvgRateStat(void *self, const char *pchName, float flCountThisSession, double dSessionLength) { return 1; }
int SteamAPI_ISteamUserStats_GetAchievement(void *self, const char *pchName, int *pbAchieved) {
    if (pbAchieved) *pbAchieved = 0;
    return 1;
}
int SteamAPI_ISteamUserStats_SetAchievement(void *self, const char *pchName) { return 1; }
int SteamAPI_ISteamUserStats_ClearAchievement(void *self, const char *pchName) { return 1; }
int SteamAPI_ISteamUserStats_GetAchievementAndUnlockTime(void *self, const char *pchName, int *pbAchieved, uint32_t *punUnlockTime) {
    if (pbAchieved) *pbAchieved = 0;
    if (punUnlockTime) *punUnlockTime = 0;
    return 1;
}
int SteamAPI_ISteamUserStats_StoreStats(void *self) { return 1; }
int SteamAPI_ISteamUserStats_IndicateAchievementProgress(void *self, const char *pchName, uint32_t nCurProgress, uint32_t nMaxProgress) { return 1; }
int SteamAPI_ISteamUserStats_GetUserStatInt32(void *self, uint64_t steamIDUser, const char *pchName, int32_t *pData) {
    if (pData) *pData = 0;
    return 0;
}
int SteamAPI_ISteamUserStats_GetUserStatFloat(void *self, uint64_t steamIDUser, const char *pchName, float *pData) {
    if (pData) *pData = 0.0f;
    return 0;
}
int SteamAPI_ISteamUserStats_GetUserAchievement(void *self, uint64_t steamIDUser, const char *pchName, int *pbAchieved) {
    if (pbAchieved) *pbAchieved = 0;
    return 0;
}
int SteamAPI_ISteamUserStats_GetUserAchievementAndUnlockTime(void *self, uint64_t steamIDUser, const char *pchName, int *pbAchieved, uint32_t *punUnlockTime) {
    if (pbAchieved) *pbAchieved = 0;
    if (punUnlockTime) *punUnlockTime = 0;
    return 0;
}
int SteamAPI_ISteamUserStats_ResetAllStats(void *self, int bAchievementsToo) { return 1; }
uint32_t SteamAPI_ISteamUserStats_GetNumAchievements(void *self) { return 0; }
const char *SteamAPI_ISteamUserStats_GetAchievementName(void *self, uint32_t iAchievement) { return ""; }
int SteamAPI_ISteamUserStats_GetAchievementDisplayAttribute(void *self, const char *pchName, const char *pchKey) { return 0; }
uint64_t SteamAPI_ISteamUserStats_RequestUserStats(void *self, uint64_t steamIDUser) { return 0; }
int SteamAPI_ISteamUserStats_GetAchievementAchievedPercent(void *self, const char *pchName, float *pflPercent) {
    if (pflPercent) *pflPercent = 0.0f;
    return 0;
}
uint64_t SteamAPI_ISteamUserStats_RequestGlobalAchievementPercentages(void *self) { return 0; }
uint64_t SteamAPI_ISteamUserStats_RequestGlobalStats(void *self, int nHistoryDays) { return 0; }
int SteamAPI_ISteamUserStats_GetGlobalStatInt64(void *self, const char *pchStatName, int64_t *pData) {
    if (pData) *pData = 0;
    return 0;
}
int SteamAPI_ISteamUserStats_GetGlobalStatDouble(void *self, const char *pchStatName, double *pData) {
    if (pData) *pData = 0.0;
    return 0;
}
int SteamAPI_ISteamUserStats_GetGlobalStatHistoryInt64(void *self, const char *pchStatName, int64_t *pData, uint32_t cubData) { return 0; }
int SteamAPI_ISteamUserStats_GetGlobalStatHistoryDouble(void *self, const char *pchStatName, double *pData, uint32_t cubData) { return 0; }
int SteamAPI_ISteamUserStats_GetAchievementIcon(void *self, const char *pchName) { return 0; }
uint64_t SteamAPI_ISteamUserStats_FindOrCreateLeaderboard(void *self, const char *pchLeaderboardName, int eLeaderboardSortMethod, int eLeaderboardDisplayType) { return 0; }
uint64_t SteamAPI_ISteamUserStats_FindLeaderboard(void *self, const char *pchLeaderboardName) { return 0; }
int SteamAPI_ISteamRemoteStorage_FileWrite(void *self, const char *pchFile, const void *pvData, int32_t cubData) { return 0; }
int32_t SteamAPI_ISteamRemoteStorage_FileRead(void *self, const char *pchFile, void *pvData, int32_t cubDataToRead) { return 0; }
int SteamAPI_ISteamRemoteStorage_FileExists(void *self, const char *pchFile) { return 0; }
int SteamAPI_ISteamRemoteStorage_FileDelete(void *self, const char *pchFile) { return 0; }
int32_t SteamAPI_ISteamRemoteStorage_GetFileSize(void *self, const char *pchFile) { return 0; }
int32_t SteamAPI_ISteamRemoteStorage_GetFileCount(void *self) { return 0; }
const char *SteamAPI_ISteamRemoteStorage_GetFileNameAndSize(void *self, int iFile, int32_t *pnFileSizeInBytes) {
    if (pnFileSizeInBytes) *pnFileSizeInBytes = 0;
    return "";
}
int SteamAPI_ISteamRemoteStorage_IsCloudEnabledForAccount(void *self) { return 0; }
int SteamAPI_ISteamRemoteStorage_IsCloudEnabledForApp(void *self) { return 0; }
void SteamAPI_ISteamRemoteStorage_SetCloudEnabledForApp(void *self, int bEnabled) {}
int64_t SteamAPI_ISteamRemoteStorage_GetFileTimestamp(void *self, const char *pchFile) { return 0; }
int SteamAPI_ISteamRemoteStorage_GetQuota(void *self, uint64_t *pnTotalBytes, uint64_t *puAvailableBytes) {
    if (pnTotalBytes) *pnTotalBytes = 0;
    if (puAvailableBytes) *puAvailableBytes = 0;
    return 0;
}
uint64_t SteamAPI_ISteamRemoteStorage_FileWriteAsync(void *self, const char *pchFile, const void *pvData, uint32_t cubData) { return 0; }
uint64_t SteamAPI_ISteamRemoteStorage_FileReadAsync(void *self, const char *pchFile, uint32_t nOffset, uint32_t cubToRead) { return 0; }
int SteamAPI_ISteamRemoteStorage_FileReadAsyncComplete(void *self, uint64_t hReadCall, void *pvBuffer, uint32_t cubToRead) { return 0; }
int SteamAPI_ISteamRemoteStorage_FileForget(void *self, const char *pchFile) { return 0; }
int SteamAPI_ISteamRemoteStorage_FilePersisted(void *self, const char *pchFile) { return 0; }
int SteamAPI_ISteamRemoteStorage_GetSyncPlatforms(void *self, const char *pchFile) { return 0; }
int SteamAPI_ISteamRemoteStorage_BeginFileWriteBatch(void *self) { return 0; }
int SteamAPI_ISteamRemoteStorage_EndFileWriteBatch(void *self) { return 0; }
typedef struct { int eMode; float x; float y; int bActive; } InputAnalogActionData_t;
typedef struct { int bState; int bActive; } InputDigitalActionData_t;
typedef struct { float rotQuatX; float rotQuatY; float rotQuatZ; float rotQuatW; float posAccelX; float posAccelY; float posAccelZ; float rotVelX; float rotVelY; float rotVelZ; } InputMotionData_t;

int SteamAPI_ISteamInput_Init(void *self, int bExplicitlyCallRunFrame) { return 1; }
int SteamAPI_ISteamInput_Shutdown(void *self) { return 1; }
void SteamAPI_ISteamInput_RunFrame(void *self, int bReservedValue) {}
int SteamAPI_ISteamInput_GetConnectedControllers(void *self, uint64_t *handlesOut) { return 0; }
void SteamAPI_ISteamInput_ActivateActionSet(void *self, uint64_t inputHandle, uint64_t actionSetHandle) {}
void SteamAPI_ISteamInput_ActivateActionSetLayer(void *self, uint64_t inputHandle, uint64_t actionSetLayerHandle) {}
void SteamAPI_ISteamInput_DeactivateActionSetLayer(void *self, uint64_t inputHandle, uint64_t actionSetLayerHandle) {}
void SteamAPI_ISteamInput_DeactivateAllActionSetLayers(void *self, uint64_t inputHandle) {}
uint64_t SteamAPI_ISteamInput_GetActionSetHandle(void *self, const char *pszActionSetName) { return 0; }
int SteamAPI_ISteamInput_GetActiveActionSetLayers(void *self, uint64_t inputHandle, uint64_t *handlesOut) { return 0; }
uint64_t SteamAPI_ISteamInput_GetCurrentActionSet(void *self, uint64_t inputHandle) { return 0; }
uint64_t SteamAPI_ISteamInput_GetAnalogActionHandle(void *self, const char *pszActionName) { return 0; }
InputAnalogActionData_t SteamAPI_ISteamInput_GetAnalogActionData(void *self, uint64_t inputHandle, uint64_t analogActionHandle) {
    InputAnalogActionData_t data = {0}; return data;
}
int SteamAPI_ISteamInput_GetAnalogActionOrigins(void *self, uint64_t inputHandle, uint64_t actionSetHandle, uint64_t analogActionHandle, int *originsOut) { return 0; }
uint64_t SteamAPI_ISteamInput_GetDigitalActionHandle(void *self, const char *pszActionName) { return 0; }
InputDigitalActionData_t SteamAPI_ISteamInput_GetDigitalActionData(void *self, uint64_t inputHandle, uint64_t digitalActionHandle) {
    InputDigitalActionData_t data = {0}; return data;
}
int SteamAPI_ISteamInput_GetDigitalActionOrigins(void *self, uint64_t inputHandle, uint64_t actionSetHandle, uint64_t digitalActionHandle, int *originsOut) { return 0; }
const char *SteamAPI_ISteamInput_GetGlyphForActionOrigin_Legacy(void *self, int eOrigin) { return ""; }
const char *SteamAPI_ISteamInput_GetGlyphForXboxOrigin(void *self, int eOrigin) { return ""; }
const char *SteamAPI_ISteamInput_GetGlyphPNGForActionOrigin(void *self, int eOrigin, int eSize, uint32_t unFlags) { return ""; }
const char *SteamAPI_ISteamInput_GetGlyphSVGForActionOrigin(void *self, int eOrigin, uint32_t unFlags) { return ""; }
const char *SteamAPI_ISteamInput_GetStringForActionOrigin(void *self, int eOrigin) { return ""; }
const char *SteamAPI_ISteamInput_GetStringForAnalogActionName(void *self, uint64_t eActionHandle) { return ""; }
const char *SteamAPI_ISteamInput_GetStringForDigitalActionName(void *self, uint64_t eActionHandle) { return ""; }
const char *SteamAPI_ISteamInput_GetStringForXboxOrigin(void *self, int eOrigin) { return ""; }
int SteamAPI_ISteamInput_GetInputTypeForHandle(void *self, uint64_t inputHandle) { return 0; }
uint64_t SteamAPI_ISteamInput_GetControllerForGamepadIndex(void *self, int nIndex) { return 0; }
int SteamAPI_ISteamInput_GetGamepadIndexForController(void *self, uint64_t ulControllerHandle) { return -1; }
InputMotionData_t SteamAPI_ISteamInput_GetMotionData(void *self, uint64_t inputHandle) {
    InputMotionData_t data = {0}; return data;
}
uint32_t SteamAPI_ISteamInput_GetRemotePlaySessionID(void *self, uint64_t inputHandle) { return 0; }
uint16_t SteamAPI_ISteamInput_GetSessionInputConfigurationSettings(void *self) { return 0; }
int SteamAPI_ISteamInput_GetActionOriginFromXboxOrigin(void *self, uint64_t inputHandle, int eOrigin) { return 0; }
int SteamAPI_ISteamInput_TranslateActionOrigin(void *self, int eDestinationInputType, int eSourceOrigin) { return 0; }
int SteamAPI_ISteamInput_GetDeviceBindingRevision(void *self, uint64_t inputHandle, int *pMajor, int *pMinor) {
    if (pMajor) *pMajor = 0; if (pMinor) *pMinor = 0; return 0;
}
int SteamAPI_ISteamInput_ShowBindingPanel(void *self, uint64_t inputHandle) { return 0; }
void SteamAPI_ISteamInput_SetLEDColor(void *self, uint64_t inputHandle, uint8_t nColorR, uint8_t nColorG, uint8_t nColorB, uint32_t nFlags) {}
void SteamAPI_ISteamInput_Legacy_TriggerHapticPulse(void *self, uint64_t inputHandle, int eTargetPad, uint16_t usDurationMicroSec) {}
void SteamAPI_ISteamInput_Legacy_TriggerRepeatedHapticPulse(void *self, uint64_t inputHandle, int eTargetPad, uint16_t usDurationMicroSec, uint16_t usOffMicroSec, uint16_t unRepeat, uint32_t nFlags) {}
void SteamAPI_ISteamInput_TriggerVibration(void *self, uint64_t inputHandle, uint16_t usLeftSpeed, uint16_t usRightSpeed) {}
void SteamAPI_ISteamInput_TriggerVibrationExtended(void *self, uint64_t inputHandle, uint16_t usLeftSpeed, uint16_t usRightSpeed, uint16_t usLeftTriggerSpeed, uint16_t usRightTriggerSpeed) {}
void SteamAPI_ISteamInput_TriggerSimpleHapticEvent(void *self, uint64_t inputHandle, int eHapticLocation, uint8_t nIntensity, char nGainDB, uint8_t nOtherIntensity, char nOtherGainDB) {}
void SteamAPI_ISteamInput_SetDualSenseTriggerEffect(void *self, uint64_t inputHandle, void *pParam) {}
int SteamAPI_ISteamInput_SetInputActionManifestFilePath(void *self, const char *pchInputActionManifestAbsolutePath) { return 0; }
void SteamAPI_ISteamInput_StopAnalogActionMomentum(void *self, uint64_t inputHandle, uint64_t eAction) {}
int SteamAPI_ISteamInput_BNewDataAvailable(void *self) { return 0; }
int SteamAPI_ISteamInput_BWaitForData(void *self, int bWaitForever, uint32_t unTimeout) { return 0; }
void SteamAPI_ISteamInput_EnableDeviceCallbacks(void *self) {}
void SteamAPI_ISteamInput_EnableActionEventCallbacks(void *self, void *pCallback) {}
int SteamAPI_ISteamNetworkingSockets_InitAuthentication(void *self) { return 0; }
int SteamAPI_ISteamHTMLSurface_Init(void *self) { return 1; }
int SteamAPI_ISteamHTMLSurface_Shutdown(void *self) { return 1; }
void SteamAPI_ISteamTimeline_SetTimelineStateDescription(void *self, const char *pchDescription, float flTimeDelta) {}
void SteamAPI_ISteamTimeline_ClearTimelineStateDescription(void *self, float flTimeDelta) {}
void SteamAPI_ISteamTimeline_AddTimelineEvent(void *self, const char *pchIcon, const char *pchTitle, const char *pchDescription, uint32_t unPriority, float flStartOffsetSeconds, float flDurationSeconds, int ePossibleClip) {}
void SteamAPI_ISteamTimeline_SetTimelineGameMode(void *self, int eMode) {}
void SteamAPI_gameserveritem_t_Construct(void *self) {}
const char *SteamAPI_gameserveritem_t_GetName(void *self) { return ""; }
void SteamAPI_gameserveritem_t_SetName(void *self, const char *pName) {}
void *SteamInternal_GameServer_Init_V2(uint32_t unIP, uint16_t usGamePort, uint16_t usQueryPort,
    int eServerMode, const char *pchVersionString) { return &dummy_steam_object; }
