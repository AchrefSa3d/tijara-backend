namespace TijaraApi.Models;

// ════════════════════════════════════════════════════════════
//  ENTITÉS  (correspondent aux tables DealsDB26)
// ════════════════════════════════════════════════════════════

public class User
{
    public long    IdUser                   { get; set; }
    public string? Username                 { get; set; }
    public string? FirstName                { get; set; }
    public string? LastName                 { get; set; }
    public string? BirthDate               { get; set; }
    public string? Gender                  { get; set; }
    public string? City                    { get; set; }
    public string? Email                   { get; set; }
    public string? Telephone               { get; set; }
    public string? Password               { get; set; }
    public int?    IdRole                  { get; set; }
    public string? ProfilePicture         { get; set; }
    public string? CreationDate           { get; set; }
    public int?    IsVerified             { get; set; }
    public int?    IsPremuim              { get; set; }
    public int?    IsBusinessAccount      { get; set; }
    public long?   IdState               { get; set; }
    public long?   IdCountry             { get; set; }
    public string? Location              { get; set; }
    public string? LastConnection        { get; set; }
    public int?    Active               { get; set; }
    public int?    EmailConfirmed       { get; set; }
    public string? FacebookId           { get; set; }
    // colonnes jointes
    public string? RoleUser             { get; set; }
    public string? StateName            { get; set; }
    public string? CountryName          { get; set; }
    public int?    ActiveDeals          { get; set; }
    public string? FollowedAt           { get; set; }
}

public class Deal
{
    public int     IdDeal          { get; set; }
    public string? TitleDeal       { get; set; }
    public string? DescriptionDeal { get; set; }
    public string? DetailsDeal     { get; set; }
    public string? PriceDeal       { get; set; }
    public string? DiscountDeal    { get; set; }
    public string? Quantity        { get; set; }
    public string? DatePublication { get; set; }
    public string? DateEnd         { get; set; }
    public string? ImageDeal       { get; set; }
    public int?    Idtypecat       { get; set; }
    public int?    IdCateg         { get; set; }
    public int?    IdUser          { get; set; }
    public long?   IdState         { get; set; }
    public long?   IdPrize         { get; set; }
    public string? LocationDeal    { get; set; }
    public int?    Active          { get; set; }
    public string? Colors          { get; set; }
    public int?    Likes           { get; set; }
    public string? Telephone       { get; set; }
    public string? Email           { get; set; }
    public string? Ownerdeals      { get; set; }
    public string? Brand           { get; set; }
    public string? StartDate       { get; set; }
    // colonnes jointes
    public string? CategoryName    { get; set; }
    public string? VendorName      { get; set; }
    public string? ShopName        { get; set; }
    public double? AvgRating       { get; set; }
    public int?    ReviewCount     { get; set; }
}

public class Product
{
    public long    IdProduct          { get; set; }
    public string? CodeBarProduct     { get; set; }
    public string? TitleProduct       { get; set; }
    public string? DescriptionProduct { get; set; }
    public int?    QuantityProduct    { get; set; }
    public string? ColorProduct       { get; set; }
    public string? PriceProduct       { get; set; }
    public string? ImageProduct       { get; set; }
    public long?   IdCateorie         { get; set; }
    public long?   IdUser             { get; set; }
    public long?   IdCountrie         { get; set; }
    public int?    Active             { get; set; }
    // colonnes jointes
    public string? CategoryName       { get; set; }
    public string? VendorName         { get; set; }
}

public class Category
{
    public int     IdCateg      { get; set; }
    public string? TitleEn     { get; set; }
    public string? TitleFr     { get; set; }
    public string? TitleAr     { get; set; }
    public string? Description { get; set; }
    public string? Image       { get; set; }
    public int?    Idtypecat   { get; set; }
    public int?    Active      { get; set; }
    // jointure
    public string? TypeTitle   { get; set; }
}

public class TypeCategory
{
    public int     Idtypecat { get; set; }
    public string? Title     { get; set; }
}

public class Order
{
    public long      IdOrder         { get; set; }
    public long?     IdUser          { get; set; }
    public long?     IdDeal          { get; set; }
    public long?     IdState         { get; set; }
    public DateTime? DateTimeCommand { get; set; }
    public int?      Active          { get; set; }
    // colonnes jointes
    public string?   ClientName     { get; set; }
    public string?   ClientEmail    { get; set; }
    public string?   DealTitle      { get; set; }
    public string?   DealPrice      { get; set; }
    public string?   VendorName     { get; set; }
}

public class OrderDetail
{
    public long    IdOrderDeatils  { get; set; }
    public long?   IdUser          { get; set; }
    public long?   IdProduct       { get; set; }
    public long?   IdOrder         { get; set; }
    public string? Address         { get; set; }
    public string? Email           { get; set; }
    public string? Telephone       { get; set; }
    public string? FirstName       { get; set; }
    public string? LastName        { get; set; }
    public int?    Quantity        { get; set; }
    public string? TotalAmount     { get; set; }
    public string? DateTimeCommand { get; set; }
    public int?    Active          { get; set; }
}

public class Chat
{
    public int      IdChat         { get; set; }
    public long?    IdUserSender   { get; set; }
    public long?    IdUserReciver  { get; set; }
    public DateTime? CreatedAt     { get; set; }
    public int?     Active         { get; set; }
    // colonnes jointes
    public string?  SenderName    { get; set; }
    public string?  ReceiverName  { get; set; }
    public string?  LastMessage   { get; set; }
    public int      UnreadCount   { get; set; }
}

public class ChatMessage
{
    public long      IdChatMessage { get; set; }
    public long?     IdChat        { get; set; }
    public string?   Message       { get; set; }
    public DateTime? CreateDate    { get; set; }
    public long?     IdUserSender  { get; set; }
    public int?      Active        { get; set; }
    // colonnes jointes
    public string?   SenderName   { get; set; }
}

public class Ad
{
    public long    IdAd              { get; set; }
    public string? TitleAd          { get; set; }
    public string? DescriptionAd    { get; set; }
    public string? DetailsAd        { get; set; }
    public string? PriceAd          { get; set; }
    public string? DatePublication  { get; set; }
    public string? ImageAd          { get; set; }
    public int?    IdCateg          { get; set; }
    public long?   IdUser           { get; set; }
    public int?    Active           { get; set; }
    public string? Type             { get; set; }   // 'annonce' | 'deal'
    // colonnes jointes
    public string? AuthorName       { get; set; }
    public string? CategoryName     { get; set; }
    public string? ShopName         { get; set; }   // Username du vendeur
    public int?    LikesCount       { get; set; }
    public int?    CommentsCount    { get; set; }
}

public class Report
{
    public int     IdReport       { get; set; }
    public long?   IdUser         { get; set; }
    public int?    IdCauseReport  { get; set; }
    public string? Subject        { get; set; }
    public string? Description    { get; set; }
    public string? Date           { get; set; }
    public int?    State          { get; set; }
    public string? TypeTable      { get; set; }
    public long?   IdTable        { get; set; }
    // colonnes jointes
    public string? ClientName     { get; set; }
    public string? CauseTitle     { get; set; }
}

public class Notification
{
    public long      IdNotification { get; set; }
    public long      IdUser         { get; set; }
    public string?   Type           { get; set; }
    public string?   Title          { get; set; }
    public string?   Message        { get; set; }
    public string?   Link           { get; set; }
    public bool      IsRead         { get; set; }
    public DateTime? CreatedAt      { get; set; }
    public long?     IdReference    { get; set; }
}

public class RatingRecord
{
    public long    IdRating      { get; set; }
    public long?   IdUser        { get; set; }
    public long?   Rating        { get; set; }
    public string? CommentRating { get; set; }
    public string? Date          { get; set; }
    public string? TableName     { get; set; }
    public long?   IdTable       { get; set; }
    public int?    Active        { get; set; }
    // colonnes jointes
    public string? AuthorName   { get; set; }
}

public class CauseReport
{
    public int     IdCauseReport  { get; set; }
    public string? TitleCauseEn   { get; set; }
    public string? TitleCauseFr   { get; set; }
    public string? TitleCauseAr   { get; set; }
    public string? GroupName      { get; set; }
    public int?    Active         { get; set; }
}

public class Wallet
{
    public int      IdWallet       { get; set; }
    public long?    IdUser         { get; set; }
    public long?    NbrJeton       { get; set; }
    public decimal? MoneyBudget    { get; set; }
    public decimal? MoneyBlocked   { get; set; }
    public decimal? MoneyTransfered { get; set; }
    public int?     Active         { get; set; }
}

// ════════════════════════════════════════════════════════════
//  DTOs  (requêtes entrantes)
// ════════════════════════════════════════════════════════════

public class LoginRequest
{
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";
}

public class RegisterRequest
{
    public string  Email          { get; set; } = "";
    public string  Password       { get; set; } = "";
    public string? FirstName      { get; set; }
    public string? LastName       { get; set; }
    public string? Telephone      { get; set; }
    public string? Phone          { get; set; }  // alias frontend
    public string? City           { get; set; }
    public string? Username       { get; set; }
    public string? ShopName       { get; set; }  // shop_name (alias vendor)
    public string? CompanyNumber  { get; set; }  // company_number
    public string  Role           { get; set; } = "user"; // "user" | "vendor"
    public string? BirthDate      { get; set; }  // date de naissance (client)
    public string? Gender         { get; set; }  // genre: "homme" | "femme" | "autre"
}

public class UpdateProfileRequest
{
    public string? FirstName      { get; set; }
    public string? LastName       { get; set; }
    public string? Telephone      { get; set; }
    public string? Phone          { get; set; }  // alias frontend
    public string? Location       { get; set; }
    public string? Address        { get; set; }  // alias frontend
    public string? City           { get; set; }  // ville (texte libre)
    public string? ProfilePicture { get; set; }
    public string? Username       { get; set; }
    public string? ShopName       { get; set; }  // shop_name → Username (vendeur)
    public string? CompanyNumber  { get; set; }  // company_number (futur usage)
    public long?   IdState        { get; set; }
    public long?   IdCountry      { get; set; }
    public string? BirthDate      { get; set; }
    public string? Gender         { get; set; }
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword     { get; set; } = "";
}

public class CreateDealRequest
{
    public string? TitleDeal       { get; set; }
    public string? DescriptionDeal { get; set; }
    public string? DetailsDeal     { get; set; }
    public string? PriceDeal       { get; set; }
    public string? DiscountDeal    { get; set; }
    public string? Quantity        { get; set; }
    public string? ImageDeal       { get; set; }
    public string? VideoAd         { get; set; }
    public int?    IdCateg         { get; set; }
    public string? Colors          { get; set; }
    public string? Brand           { get; set; }
    public string? Telephone       { get; set; }
    public string? DateEnd         { get; set; }
}

public class CreateAdRequest
{
    public string? TitleAd         { get; set; }
    public string? Title           { get; set; }   // alias: frontend sends "title"
    public string? DescriptionAd   { get; set; }
    public string? Content         { get; set; }   // alias: frontend sends "content"
    public string? DetailsAd       { get; set; }
    public string? PriceAd         { get; set; }
    public string? Price           { get; set; }   // alias: frontend sends "price"
    public string? ImageAd         { get; set; }
    public string? ImageUrl        { get; set; }   // alias: frontend sends "image_url"
    public int?    IdCateg         { get; set; }
    public int?    CategoryId      { get; set; }   // alias: frontend sends "category_id"
    public string? Type            { get; set; }   // 'annonce' | 'deal'
}

public class CreateReportRequest
{
    public int?    IdCauseReport { get; set; }
    public string? Subject       { get; set; }
    public string? Description   { get; set; }
    public string? TypeTable     { get; set; }
    public long?   IdTable       { get; set; }
}

public class StatusRequest
{
    public string Status { get; set; } = "";
}

public class ReasonRequest
{
    public string? Reason { get; set; }
}

public class RatingRequest
{
    public int     Rating  { get; set; }
    public string? Comment { get; set; }
}

public class MessageRequest
{
    public string Content { get; set; } = "";
}

public class StartChatRequest
{
    public long   IdUserReciver { get; set; }  // id_user_reciver
    public long   VendorId      { get; set; }  // vendor_id  (alias frontend)
    public string Content       { get; set; } = "";
}

public class UserFollow
{
    public long     IdFollow   { get; set; }
    public long     IdUser     { get; set; }
    public long     IdVendor   { get; set; }
    public DateTime CreatedAt  { get; set; }
}

public class ResendConfirmRequest
{
    public string Email { get; set; } = "";
}

public class FacebookLoginRequest
{
    public string AccessToken { get; set; } = "";
    public string? Email      { get; set; }
}

public class GoogleLoginRequest
{
    public string Credential { get; set; } = "";
}

// ─── Admin Settings ────────────────────────────────────────────────
public class AdminSettingRow
{
    public string Key   { get; set; } = "";
    public string? Value { get; set; }
}

public class PointPacket
{
    public long     IdPacket    { get; set; }
    public string   Title       { get; set; } = "";
    public string?  Description { get; set; }
    public int      PointsCount { get; set; }
    public decimal  Price       { get; set; }
    public decimal  Discount    { get; set; }
    public bool     Active      { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
}

public class PointPacketRequest
{
    public string   Title       { get; set; } = "";
    public string?  Description { get; set; }
    public int      PointsCount { get; set; }
    public decimal  Price       { get; set; }
    public decimal  Discount    { get; set; }
    public bool     Active      { get; set; } = true;
}

// ═══════════════ LOT 1 — CATALOG & MODERATION ═══════════════

public class Brand
{
    public long     IdBrand     { get; set; }
    public string   Title       { get; set; } = "";
    public string?  Description { get; set; }
    public string?  Image       { get; set; }
    public bool     Active      { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
}

public class Country
{
    public long     IdCountry { get; set; }
    public string   Title     { get; set; } = "";
    public string?  Flag      { get; set; }
    public string?  Code      { get; set; }
    public string?  PhoneCode { get; set; }
    public bool     Active    { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class City
{
    public long     IdCity    { get; set; }
    public string   Title     { get; set; } = "";
    public long?    IdCountry { get; set; }
    public string?  TitleEn   { get; set; }
    public string?  TitleAr   { get; set; }
    public string?  Image     { get; set; }
    public bool     Active    { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Cause
{
    public long     IdCause     { get; set; }
    public string   Title       { get; set; } = "";
    public string?  Description { get; set; }
    public string?  Email       { get; set; }
    public string?  Type        { get; set; }
    public bool     Active      { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
}

public class Coupon
{
    public long      IdCoupon       { get; set; }
    public string    Title          { get; set; } = "";
    public string?   Description    { get; set; }
    public DateTime? DateStart      { get; set; }
    public DateTime? DateEnd        { get; set; }
    public decimal   Price          { get; set; }
    public int       NumberOfCoupon { get; set; }
    public int       Used           { get; set; }
    public bool      Active         { get; set; } = true;
    public DateTime  CreatedAt      { get; set; }
}

public class Prize
{
    public long      IdPrize     { get; set; }
    public string    Title       { get; set; } = "";
    public string?   Description { get; set; }
    public string?   Image       { get; set; }
    public DateTime? DatePrize   { get; set; }
    public long?     IdUser      { get; set; }
    public bool      Active      { get; set; } = true;
    public DateTime  CreatedAt   { get; set; }
}

// ═══════════════ LOT 2 — WINNERS / WISHLISTS / REVIEWS ═══════════════

public class Winner
{
    public long      IdWinner  { get; set; }
    public long?     IdUser    { get; set; }
    public long?     IdPrize   { get; set; }
    public long?     IdOrder   { get; set; }
    public string?   FullName  { get; set; }
    public string?   Email     { get; set; }
    public string?   Phone     { get; set; }
    public string?   Note      { get; set; }
    public DateTime  WonAt     { get; set; }
    public bool      Active    { get; set; } = true;
    public DateTime  CreatedAt { get; set; }
    // joined
    public string?   UserName  { get; set; }
    public string?   PrizeTitle { get; set; }
}

public class WinnerRequest
{
    public long?   IdUser   { get; set; }
    public long?   IdPrize  { get; set; }
    public long?   IdOrder  { get; set; }
    public string? FullName { get; set; }
    public string? Email    { get; set; }
    public string? Phone    { get; set; }
    public string? Note     { get; set; }
    public bool    Active   { get; set; } = true;
}

public class WishlistItem
{
    public long     IdWish    { get; set; }
    public long     IdUser    { get; set; }
    public long     TargetId  { get; set; }
    public DateTime CreatedAt { get; set; }
    // joined
    public string?  Title     { get; set; }
    public string?  Image     { get; set; }
    public string?  Price     { get; set; }
}

public class ReviewRecord
{
    public long      IdReview    { get; set; }
    public long      IdUser      { get; set; }
    public string    TargetType  { get; set; } = "";
    public long      TargetId    { get; set; }
    public int       Rating      { get; set; }
    public string?   Comment     { get; set; }
    public bool      Active      { get; set; }
    public DateTime  CreatedAt   { get; set; }
    // joined
    public string?   AuthorName  { get; set; }
}

public class ReviewRequest
{
    public int     Rating  { get; set; }
    public string? Comment { get; set; }
}

public class BoostPack
{
    public long     IdBoost     { get; set; }
    public string   Title       { get; set; } = "";
    public decimal  Price       { get; set; }
    public decimal  Discount    { get; set; }
    public int      MaxDuration { get; set; } = 7;
    public bool     Sliders     { get; set; }
    public bool     SideBar     { get; set; }
    public bool     Footer      { get; set; }
    public bool     RelatedPost { get; set; }
    public bool     FirstLogin  { get; set; }
    public int      OrdersCount { get; set; }
    public bool     Links       { get; set; }
    public bool     Active      { get; set; } = true;
    public DateTime CreatedAt   { get; set; }
}
