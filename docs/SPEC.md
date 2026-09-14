# TechVault

## 1. Product Overview

**TechVault** là một digital museum + technology specification database tập trung vào hai nhóm thiết bị:

- Phones
- Computers

Concept:

> **GSMArena + Wikipedia + Digital Technology Museum**

Website có hai trải nghiệm chính:

1. **Explore / Museum**
   - Khám phá lịch sử điện thoại và máy tính.
   - Browse theo timeline.
   - Xem các thiết bị iconic.
   - Đọc câu chuyện và bối cảnh lịch sử.
   - Interactive UI / animation / optional 3D model.

2. **Specs / Database**
   - Xem thông số kỹ thuật.
   - Search.
   - Filter.
   - Compare.
   - Brand pages.
   - Category pages.
   - SEO landing pages.

Mục tiêu dài hạn:

- SEO.
- Organic traffic.
- Advertising.
- Affiliate.
- Database có thể tăng lên hàng nghìn thiết bị.

---

# 2. Product Positioning

TechVault không phải chỉ là một specs database.

Core idea:

```txt
Search utility
      ↓
Structured specs
      ↓
Historical context
      ↓
Timeline
      ↓
Museum exploration
```

Một visitor có thể đến từ Google bằng:

```txt
Nokia 3310 specs
```

sau đó tiếp tục khám phá:

```txt
Nokia 3310
    ↓
Nokia timeline
    ↓
Phones of the 2000s
    ↓
Nokia 3310 vs Nokia 3210
    ↓
Tech Museum
```

---

# 3. Main Goals

## Primary technical goals

Project được sử dụng để học:

```txt
React / Next.js
ASP.NET Core
PostgreSQL
Redis
Object Storage
Docker
Linux
Nginx
DNS
TLS / HTTPS
CI/CD
SEO
Caching
Logging
Monitoring
Backup
Software Architecture
Design Patterns
```

## Product goals

Visitor có thể:

- Browse phones.
- Browse computers.
- Search devices.
- Browse theo brand.
- Browse theo year / era.
- View product detail.
- View specifications.
- View product history.
- Explore timeline.
- Compare related devices.
- Explore curated museum collections.

Admin có thể:

- Create/edit devices.
- Manage specifications.
- Manage brands.
- Manage categories.
- Upload media.
- Manage collections.
- Control SEO metadata.
- Publish/unpublish content.

---

# 4. Initial Product Scope

TechVault v1 chỉ hỗ trợ:

```txt
Phones
Computers
```

Không support ở giai đoạn hiện tại:

```txt
Game Consoles
Audio Devices
Cameras
CPUs
GPUs
Monitors
Keyboards
Mice
Wearables
Networking Devices
```

Architecture vẫn phải generic để có thể thêm các category mới sau này mà không redesign database.

---

# 5. Category Structure

Initial hierarchy:

```txt
Phones
├── Feature Phones
└── Smartphones

Computers
├── Desktop Computers
├── Laptops
├── All-in-One Computers
└── Workstations
```

Primary navigation:

```txt
Explore
Phones
Computers
Timeline
Compare
```

---

# 6. Product Identity

Suggested positioning:

> **TechVault — Explore the evolution of phones and computers.**

Alternative:

> **Explore the devices that shaped how we communicate and compute.**

Tone:

```txt
Historical
Technical
Modern
Curated
Minimal
Premium
```

Không làm quá cyberpunk.

---

# 7. Frontend Tech Stack

Use:

```txt
Next.js
React
TypeScript
Tailwind CSS
TanStack Query
Zustand
React Hook Form
Zod
```

Next.js được sử dụng vì SEO là first-class requirement.

Rendering strategy:

```txt
SEO pages
→ SSR / SSG / ISR

Museum experiences
→ Server Components + Client Components where needed

Admin
→ client-heavy
```

Optional:

```txt
Framer Motion
Three.js
React Three Fiber
```

3D không phải requirement bắt buộc.

---

# 8. Backend Tech Stack

Use:

```txt
ASP.NET Core Web API
.NET 10+
Entity Framework Core
PostgreSQL
Redis
FluentValidation
Serilog
OpenTelemetry
```

Backend architecture:

> **Modular Monolith + Clean Architecture boundaries + Vertical Slice Architecture**

Use:

```txt
CQRS-lite
Dependency Injection
Strategy Pattern
Query Object Pattern
Result Pattern
Value Objects
Cache-Aside Pattern
Factory Pattern where useful
```

Do not use by default:

```txt
Microservices
Kafka
Kubernetes
Event Sourcing
Generic Repository
Custom Unit of Work
MediatR
CQRS with separate databases
```

---

# 9. Backend Architectural Style

TechVault sử dụng một **Modular Monolith**.

Toàn bộ backend chạy trong một deployable ASP.NET Core application.

```txt
TechVault Backend

Devices
Brands
Categories
Specifications
Comparisons
Timeline
Collections
Media
```

Các module được tách rõ về code nhưng không deploy riêng.

Điều này cho phép:

```txt
Simple deployment
Simple debugging
Single database
Single transaction boundary
Clear module boundaries
```

và vẫn có khả năng tách service sau này nếu thực sự cần.

---

# 10. Backend Layers

Structure:

```txt
TechVault.Api
TechVault.Application
TechVault.Domain
TechVault.Infrastructure
```

Dependency direction:

```txt
Api
 │
 ▼
Application
 │
 ▼
Domain

Infrastructure
     │
     ├── implements Application abstractions
     └── references Domain
```

Domain không được reference:

```txt
ASP.NET
EF Core
Redis
S3
HTTP
Nginx
Infrastructure
```

---

# 11. Domain Layer

`TechVault.Domain` chứa:

```txt
Entities
Value Objects
Enums
Domain Rules
Domain Services when required
```

Suggested modules:

```txt
TechVault.Domain/

Common/
└── BaseEntity.cs

Devices/
Brands/
Categories/
Specifications/
Comparisons/
Collections/
Media/
```

Shared identity:

`Common/BaseEntity` là abstract class không generic, chỉ chứa `Guid Id` được tạo khi khởi tạo entity và không có public setter. Brand, Category, Device, SpecificationGroup và SpecificationDefinition kế thừa lớp này để dùng chung identity. Các trường `CreatedAt`/`UpdatedAt` vẫn thuộc Brand và Device; không thêm audit fields cho mọi entity chỉ để đưa vào base class.

DeviceSpecification giữ khóa ghép `(DeviceId, DefinitionId)`, không kế thừa BaseEntity và không thêm surrogate `Id`. BaseEntity không phải EF entity/table; không tạo `DbSet<BaseEntity>` hay persistence inheritance hierarchy. Việc dùng chung Id không thay đổi schema, không yêu cầu migration mới, và không kéo theo repository, soft delete hoặc domain events.

Example entity (simplified):

```csharp
public class Device : BaseEntity
{
    public string Name { get; private set; } = null!;

    public string Slug { get; private set; } = null!;

    public Guid BrandId { get; private set; }

    public Guid CategoryId { get; private set; }

    public DeviceStatus Status { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public void Publish()
    {
        if (Status == DeviceStatus.Published)
            return;

        Status = DeviceStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
    }
}
```

Không chứa:

```txt
Controllers
DbContext
Redis clients
AWS SDK
DTOs
HTTP status codes
```

---

# 12. Application Layer

`TechVault.Application` chứa các use cases.

Organize theo **Vertical Slice**, không organize toàn bộ application theo technical folder.

Good:

```txt
Devices/

├── GetDevice/
│   ├── GetDeviceQuery.cs
│   ├── GetDeviceHandler.cs
│   ├── GetDeviceResponse.cs
│   └── GetDeviceValidator.cs
│
├── SearchDevices/
│   ├── SearchDevicesQuery.cs
│   ├── SearchDevicesHandler.cs
│   └── SearchDevicesResponse.cs
│
├── CreateDevice/
│   ├── CreateDeviceCommand.cs
│   ├── CreateDeviceHandler.cs
│   └── CreateDeviceValidator.cs
│
└── PublishDevice/
    ├── PublishDeviceCommand.cs
    └── PublishDeviceHandler.cs
```

Avoid:

```txt
Services/
Repositories/
Dtos/
Validators/
Mappings/
```

khi tất cả feature bị trộn chung vào những folder này.

---

# 13. Vertical Slice Architecture

Mỗi use case nên chứa logic của riêng nó.

Ví dụ:

```txt
CreateDevice

Request
Validator
Handler
Response
```

Không cần tạo một `DeviceService` chứa 30 methods.

Prefer:

```txt
CreateDeviceHandler
UpdateDeviceHandler
GetDeviceHandler
SearchDevicesHandler
PublishDeviceHandler
```

thay vì:

```txt
DeviceService
{
    Create()
    Update()
    Search()
    Publish()
    Delete()
    Get()
    Compare()
    ...
}
```

---

# 14. CQRS-lite

Application tách conceptual flow:

```txt
Command
→ mutate state

Query
→ read state
```

Commands:

```txt
CreateDevice
UpdateDevice
PublishDevice
ArchiveDevice
AddSpecification
UploadMedia
```

Queries:

```txt
GetDevice
SearchDevices
GetBrand
GetTimeline
CompareDevices
GetCollection
```

Không dùng distributed CQRS.

Cả read và write đều dùng:

```txt
PostgreSQL
```

Không có:

```txt
separate write database
separate read database
event sourcing
message broker
```

---

# 15. MediatR Decision

MVP:

```txt
Do not use MediatR.
```

Reason:

Không cần flow:

```txt
Controller
   ↓
Mediator
   ↓
Request
   ↓
Handler
```

nếu controller có thể gọi handler trực tiếp.

Example:

```csharp
public sealed class DevicesController(
    GetDeviceHandler getDeviceHandler)
{
}
```

Có thể thêm MediatR sau này nếu pipeline behaviors thực sự mang lại lợi ích cho:

```txt
Validation
Logging
Transactions
Metrics
Authorization
```

---

# 16. Repository Pattern Decision

Do not implement:

```csharp
IRepository<T>
```

với generic methods như:

```txt
GetById
Add
Update
Delete
Find
```

EF Core đã cung cấp:

```txt
DbSet
DbContext
Change Tracking
Unit of Work semantics
```

Generic Repository sẽ tạo abstraction không cần thiết.

Application có thể depend vào:

```csharp
public interface ITechVaultDbContext
{
    DbSet<Device> Devices { get; }

    DbSet<Brand> Brands { get; }

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken);
}
```

---

# 17. Unit of Work Decision

Do not create:

```txt
IUnitOfWork
```

chỉ để wrap `DbContext`.

EF Core `DbContext` được xem là Unit of Work.

Explicit transaction chỉ dùng khi cần:

```csharp
await using var transaction =
    await db.Database.BeginTransactionAsync();

...

await db.SaveChangesAsync();

await transaction.CommitAsync();
```

---

# 18. Infrastructure Layer

`TechVault.Infrastructure` chứa implementation của external technologies.

Structure:

```txt
Infrastructure/

Persistence/
├── TechVaultDbContext.cs
├── Configurations/
├── Migrations/
└── Seed/

Caching/
├── RedisCacheService.cs
└── CacheKeys.cs

Storage/
├── S3MediaStorage.cs
└── MinioMediaStorage.cs

Search/
└── PostgreSqlSearchService.cs

BackgroundJobs/
└── Hangfire/

Observability/
```

---

# 19. API Layer

API layer phải thin.

Responsibilities:

```txt
HTTP request
Authentication / authorization
Input mapping
Call Application use case
Map Result → HTTP response
```

Không đặt business logic trong Controller.

Example:

```txt
GET /api/v1/devices/nokia-3310

Controller
    ↓
GetDeviceHandler
    ↓
Database
    ↓
Result<DeviceResponse>
    ↓
HTTP 200 / 404
```

---

# 20. Result Pattern

Application không dùng exception cho expected business outcomes.

Use:

```txt
Result<T>
```

Possible results:

```txt
Success
ValidationFailure
NotFound
Conflict
Forbidden
Unauthorized
```

Example:

```txt
Device not found
      ↓
Result.NotFound
      ↓
API
      ↓
HTTP 404
```

Exception được dùng cho unexpected failures:

```txt
database unavailable
programming error
network failure
unexpected state
```

---

# 21. Query Object Pattern

Search/browse có nhiều filter.

Example:

```csharp
public sealed record DeviceSearchQuery(
    string? Search,
    string? Brand,
    string? Category,
    int? FromYear,
    int? ToYear,
    string? Type,
    DeviceSort Sort,
    int Page,
    int PageSize);
```

Có thể sử dụng:

```txt
DeviceSearchQuery
        ↓
DeviceQueryBuilder
        ↓
IQueryable<Device>
```

Pattern này dùng cho:

```txt
/phones
/computers
/devices
/admin/devices
```

---

# 22. Strategy Pattern

Strategy Pattern chủ yếu được dùng cho comparison.

Interface:

```csharp
public interface IDeviceComparisonStrategy
{
    string ComparisonGroup { get; }

    ComparisonResult Compare(
        Device left,
        Device right);
}
```

Implementations:

```txt
PhoneComparisonStrategy
LaptopComparisonStrategy
DesktopComparisonStrategy
```

Resolver:

```txt
ComparisonStrategyResolver
      │
      ├── PHONE → PhoneComparisonStrategy
      ├── LAPTOP → LaptopComparisonStrategy
      └── DESKTOP → DesktopComparisonStrategy
```

Avoid large:

```csharp
if (type == "PHONE")
{
}
else if (type == "LAPTOP")
{
}
else if (...)
{
}
```

---

# 23. Value Object Pattern

Value Objects được sử dụng khi primitive values có domain meaning.

Candidates:

```txt
Slug
Dimensions
ReleaseDate
SpecificationValue
```

Example:

```csharp
public sealed record Dimensions(
    decimal WidthMm,
    decimal HeightMm,
    decimal DepthMm);
```

Không cần biến tất cả field thành Value Object.

Chỉ dùng khi nó thực sự encapsulate:

```txt
validation
equality
domain behavior
```

---

# 24. Factory Pattern

Factory Pattern chỉ dùng khi object creation có branching logic.

Potential use:

```txt
SpecificationValueFactory
```

Input:

```txt
SpecificationDefinition.DataType
raw value
```

Output:

```txt
TextSpecificationValue
NumberSpecificationValue
BooleanSpecificationValue
DateSpecificationValue
```

Không tạo Factory cho entity đơn giản chỉ vì pattern tồn tại.

---

# 25. Cache-Aside Pattern

Redis sử dụng Cache-Aside.

Read:

```txt
Request
   ↓
Redis
   │
   ├── Hit → return
   │
   └── Miss
         ↓
     PostgreSQL
         ↓
       Redis
         ↓
       return
```

Write:

```txt
Update PostgreSQL
      ↓
Commit
      ↓
Invalidate cache
```

PostgreSQL luôn là source of truth.

Redis không phải primary database.

---

# 26. Domain Events

Không bắt buộc trong MVP.

Future example:

```txt
DevicePublished
      │
      ├── invalidate cache
      ├── update sitemap
      ├── update search index
      └── analytics
```

Ban đầu có thể làm trực tiếp trong Application handler.

Chỉ introduce Domain Events khi side effects bắt đầu làm handler phức tạp.

---

# 27. Dependency Injection

Use built-in .NET DI.

Example:

```csharp
services.AddScoped<GetDeviceHandler>();

services.AddScoped<CreateDeviceHandler>();

services.AddScoped<IMediaStorage, S3MediaStorage>();

services.AddScoped<
    IDeviceComparisonStrategy,
    PhoneComparisonStrategy>();
```

Không cần third-party DI container.

---

# 28. Backend Folder Structure

```txt
apps/api/

src/

├── TechVault.Api/
│   ├── Controllers/
│   ├── Middleware/
│   ├── Authentication/
│   ├── Extensions/
│   └── Program.cs
│
├── TechVault.Application/
│   │
│   ├── Common/
│   │   ├── Abstractions/
│   │   ├── Results/
│   │   └── Pagination/
│   │
│   ├── Devices/
│   │   ├── CreateDevice/
│   │   ├── UpdateDevice/
│   │   ├── GetDevice/
│   │   ├── SearchDevices/
│   │   └── PublishDevice/
│   │
│   ├── Comparisons/
│   │   └── CompareDevices/
│   │
│   ├── Brands/
│   ├── Categories/
│   ├── Specifications/
│   ├── Timeline/
│   ├── Collections/
│   └── Media/
│
├── TechVault.Domain/
│   ├── Common/
│   │   └── BaseEntity.cs
│   ├── Devices/
│   ├── Brands/
│   ├── Categories/
│   ├── Specifications/
│   ├── Comparisons/
│   ├── Collections/
│   └── Media/
│
└── TechVault.Infrastructure/
    ├── Persistence/
    ├── Caching/
    ├── Storage/
    ├── Search/
    ├── BackgroundJobs/
    └── Observability/
```

---

# 29. Backend Pattern Summary

Patterns to use:

```txt
Modular Monolith
Vertical Slice Architecture
Clean Architecture boundaries
CQRS-lite
Dependency Injection
Strategy
Query Object
Result Pattern
Value Objects
Cache Aside
Factory where useful
```

Not initially:

```txt
Generic Repository
Custom Unit of Work
MediatR
Event Sourcing
Microservices
Kafka
Kubernetes
Separate CQRS databases
```

---

# 30. Storage

Images và 3D assets:

```txt
Development
→ MinIO

Production
→ S3-compatible object storage
```

Database chỉ lưu:

```txt
metadata
object key
URL
dimensions
mime type
license
credits
```

Không lưu image binary trong PostgreSQL.

---

# 31. High-Level System Architecture

```txt
                        Internet
                           │
                           ▼
                     Cloudflare
                           │
                           ▼
                        Nginx
                  ┌────────┴────────┐
                  │                 │
                  ▼                 ▼
              Next.js          ASP.NET API
                                    │
                ┌───────────────────┼───────────────────┐
                │                   │                   │
                ▼                   ▼                   ▼
           PostgreSQL             Redis           Object Storage
                                                     MinIO / S3
```

Initial production:

```txt
Ubuntu VPS

Docker Compose
├── nginx
├── frontend
├── api
├── postgres
├── redis
└── minio
```

---

# 32. Homepage

Route:

```txt
/
```

Suggested structure:

```txt
Hero
Featured Devices
Explore Phones
Explore Computers
Global Timeline
Explore by Era
Iconic Devices
Featured Collections
Recently Added
```

Hero:

```txt
Explore the evolution of technology.

Phones that connected the world.
Computers that changed how we work.

[ Explore Phones ]   [ Explore Computers ]
```

Global timeline:

```txt
1977 ───── 1984 ───── 1998 ───── 2000 ───── 2007

Apple II   Macintosh   iMac G3   Nokia 3310   iPhone
```

---

# 33. Device Detail

Route:

```txt
/devices/{slug}
```

Examples:

```txt
/devices/nokia-3310
/devices/iphone-4
/devices/macintosh-128k
/devices/imac-g3
```

Structure:

```txt
Hero
Key Specs
Overview
History
Timeline Context
Gallery
Related Devices
Compare CTA
```

---

# 34. Product Detail Design Principle

Top:

```txt
Museum
Visual
Story
Historical context
```

Bottom:

```txt
Structured specs
Compare
SEO content
Related devices
```

Goal:

```txt
Emotion + Utility
```

---

# 35. Specs Page

Route:

```txt
/devices/{slug}/specs
```

Requirements:

```txt
SSR
Fast load
Canonical URL
Structured data
Minimal animation
Indexable HTML
```

---

# 36. Phone Specification Groups

```txt
General
Design
Display
Hardware
Memory
Camera
Battery
Network
Connectivity
Software
Physical
```

Possible fields:

```txt
release_date
dimensions
weight
display_type
display_size
display_resolution
chipset
cpu
gpu
ram
storage
rear_camera
front_camera
battery_capacity
battery_type
network
wifi
bluetooth
operating_system
```

---

# 37. Computer Specification Groups

```txt
General
Processor
Graphics
Memory
Storage
Display
Connectivity
Ports
Software
Physical
Power
```

Possible fields:

```txt
release_date
cpu
gpu
ram
storage
display
display_resolution
ports
wifi
ethernet
operating_system
dimensions
weight
power_supply
battery
```

Specifications remain dynamic.

---

# 38. Browse Phones

Route:

```txt
/phones
```

Filters:

```txt
Brand
Year
Decade
Phone Type
Status
```

---

# 39. Browse Computers

Route:

```txt
/computers
```

Filters:

```txt
Brand
Year
Decade
Computer Type
Status
```

---

# 40. Brands

Route:

```txt
/brands/{slug}
```

Sections:

```txt
Brand overview
Founded
Country
Timeline
Product families
Popular devices
All devices
Related collections
```

---

# 41. Timeline

Routes:

```txt
/timeline
/timeline/phones
/timeline/computers
/timeline/nokia
/timeline/apple
```

Features:

```txt
Filter category
Filter brand
Filter year
Zoom
Horizontal desktop timeline
Vertical mobile timeline
```

---

# 42. Compare

Routes:

```txt
/compare
/compare/{device-a}-vs-{device-b}
```

Examples:

```txt
/compare/nokia-3310-vs-nokia-3210
/compare/iphone-4-vs-samsung-galaxy-s
/compare/macbook-air-2008-vs-macbook-pro-2008
```

---

# 43. Comparison Groups

Initial:

```txt
PHONE
LAPTOP
DESKTOP
ALL_IN_ONE
WORKSTATION
```

Rules:

```txt
Phone ↔ Phone
✓

Laptop ↔ Laptop
✓

Desktop ↔ Desktop
✓

Phone ↔ Laptop
✗
```

---

# 44. Comparison Logic

Only compare:

```txt
SpecificationDefinition.IsComparable = true
```

MVP:

```txt
max 2 devices
```

Future:

```txt
max 3 devices
```

Do not automatically assume:

```txt
higher number = better
```

---

# 45. Museum

Route:

```txt
/museum
```

Focus:

```txt
visual
storytelling
history
exploration
```

Difference:

```txt
/devices
→ data-driven browsing

/museum
→ discovery experience
```

---

# 46. Collections

Routes:

```txt
/collections
/collections/{slug}
```

Examples:

```txt
/collections/iconic-2000s-phones
/collections/evolution-of-the-mobile-phone
/collections/computers-that-changed-the-world
/collections/history-of-the-mac
/collections/rise-of-the-laptop
```

Collections được curate thủ công.

---

# 47. Search

API:

```txt
GET /api/v1/search?q=nokia
```

MVP:

```txt
PostgreSQL Full Text Search
```

Search:

```txt
device name
brand
aliases
model number
description
```

Không dùng Elasticsearch ở MVP.

---

# 48. Admin CMS

Route:

```txt
/admin
```

Features:

```txt
Dashboard
Devices
Brands
Categories
Specifications
Collections
Media
SEO
```

Device editor:

```txt
General
Classification
Specifications
Images
3D Model
History
Relationships
SEO
Publishing
```

---

# 49. Device Entity

```txt
Device
------
Id
Name
Slug
ShortDescription
Description
BrandId
CategoryId
ProductFamilyId?
ComparisonGroupId?
ReleaseDate?
ReleaseYear
DiscontinuedDate?
Country?
Status
HeroImageId?
Model3DAssetId?
Dimensions?
WeightGrams?
IsFeatured
IsIconic
ViewCount
CreatedAt
UpdatedAt
PublishedAt
```

---

# 50. Brand

```txt
Brand
-----
Id
Name
Slug
Description
FoundedYear?
Country?
LogoAssetId?
Website?
CreatedAt
UpdatedAt
```

---

# 51. Category

```txt
Category
--------
Id
Name
Slug
Description
ParentCategoryId?
ComparisonGroupId?
DisplayOrder
```

---

# 52. ComparisonGroup

```txt
ComparisonGroup
---------------
Id
Key
Name
Description?
```

---

# 53. Flexible Specification System

Do not create separate:

```txt
Phone
Computer
```

tables containing hundreds of category-specific columns.

Use dynamic specification definitions.

---

# 54. SpecificationGroup

```txt
SpecificationGroup
------------------
Id
Name
Key
DisplayOrder
```

---

# 55. SpecificationDefinition

```txt
SpecificationDefinition
-----------------------
Id
CategoryId?
ComparisonGroupId?
GroupId
Name
Key
DataType
Unit
DisplayOrder
IsComparable
IsFilterable
ComparisonDirection?
```

---

# 56. DeviceSpecification

```txt
DeviceSpecification
-------------------
DeviceId
DefinitionId
ValueText?
ValueNumber?
ValueBoolean?
ValueDate?
ValueJson?
```

---

# 57. Product Relationships

```txt
DeviceRelationship
------------------
DeviceId
RelatedDeviceId
RelationshipType
```

Types:

```txt
Predecessor
Successor
Variant
SameFamily
Competitor
Related
```

---

# 58. Product Family

```txt
ProductFamily
-------------
Id
BrandId
Name
Slug
Description
```

Examples:

```txt
iPhone
MacBook
ThinkPad
Nokia N Series
```

---

# 59. Media

```txt
MediaAsset
----------
Id
Type
StorageKey
Url
MimeType
Width?
Height?
FileSize
AltText
Credit?
License?
CreatedAt
```

---

# 60. 3D Models

Optional.

Formats:

```txt
.glb
.gltf
```

Requirements:

```txt
lazy-load
fallback image
do not block initial page render
```

---

# 61. SEO Strategy

Every device page should have:

```txt
Unique title
Meta description
Canonical
OpenGraph
Breadcrumb
Structured data
SSR content
```

Example:

```txt
Nokia 3310 Specs, History & Release Date | TechVault
```

---

# 62. SEO Landing Pages

Initial:

```txt
/phones
/computers
/brands/nokia
/brands/apple
/timeline/phones
/timeline/computers
/eras/1990s
/eras/2000s
```

Only create meaningful pages.

---

# 63. Programmatic SEO Principle

Do not create thousands of near-empty pages.

Prefer:

```txt
50 excellent device pages
```

over:

```txt
5,000 thin pages
```

---

# 64. Sitemap

```txt
/sitemap.xml
```

Future:

```txt
/sitemap-index.xml
sitemap-devices.xml
sitemap-brands.xml
sitemap-collections.xml
sitemap-timelines.xml
```

---

# 65. Internal Linking

Device page links to:

```txt
Brand
Category
Product family
Timeline
Related devices
Compare
Collections
```

---

# 66. Cache Strategy

Redis:

```txt
Homepage
Popular devices
Device details
Brand pages
Search suggestions
View counters
```

Suggested TTL:

```txt
Homepage         10 min
Device           30 min
Brand            60 min
Search Suggest   10 min
```

Uses Cache-Aside Pattern.

---

# 67. Analytics

Track:

```txt
Device page view
Search
Compare
Timeline interaction
Collection view
Outbound affiliate click
```

---

# 68. Background Jobs

Use Hangfire when needed.

Possible jobs:

```txt
Generate image thumbnails
Recalculate popularity
Warm cache
Process media
Generate sitemap
Scheduled publishing
```

Do not add background jobs prematurely.

---

# 69. Authentication

Public user authentication is **not MVP**.

No initial:

```txt
Registration
Favorites
Profiles
Comments
Reviews
```

Admin authentication is required.

---

# 70. Public API

Base:

```txt
/api/v1
```

Endpoints:

```txt
GET /api/v1/devices
GET /api/v1/devices/{slug}
GET /api/v1/devices/{slug}/specifications

GET /api/v1/phones
GET /api/v1/computers

GET /api/v1/brands
GET /api/v1/brands/{slug}

GET /api/v1/categories

GET /api/v1/search?q=

GET /api/v1/compare?devices=

GET /api/v1/timeline

GET /api/v1/collections
GET /api/v1/collections/{slug}
```

---

# 71. Admin API

```txt
POST   /api/v1/admin/devices
PUT    /api/v1/admin/devices/{id}
DELETE /api/v1/admin/devices/{id}

POST /api/v1/admin/media
POST /api/v1/admin/brands
POST /api/v1/admin/categories
POST /api/v1/admin/specifications
POST /api/v1/admin/collections
```

---

# 72. API Responses

Success:

```json
{
  "data": {}
}
```

Pagination:

```json
{
  "data": [],
  "pagination": {
    "page": 1,
    "pageSize": 24,
    "total": 200,
    "totalPages": 9
  }
}
```

Error:

```json
{
  "error": {
    "code": "DEVICE_NOT_FOUND",
    "message": "Device was not found.",
    "traceId": "..."
  }
}
```

---

# 73. Validation

Frontend:

```txt
Zod
```

Backend:

```txt
FluentValidation
```

Backend remains source of truth.

---

# 74. Security

Required:

```txt
HTTPS
Secure headers
Rate limiting
CORS allowlist
Input validation
Admin authorization
Upload validation
File size limits
Secret management
```

Do not expose:

```txt
PostgreSQL
Redis
MinIO admin console
```

---

# 75. Logging

Use:

```txt
Serilog
```

Fields:

```txt
Timestamp
Level
RequestPath
StatusCode
Duration
TraceId
AdminUserId?
```

Never log secrets.

---

# 76. Health Checks

```txt
/health/live
/health/ready
```

Readiness checks:

```txt
PostgreSQL
Redis
Object Storage
```

---

# 77. Observability

MVP:

```txt
Structured logs
Health checks
Basic metrics
```

Future:

```txt
OpenTelemetry
Prometheus
Grafana
Loki
```

---

# 78. Repository Structure

```txt
techvault/

apps/
├── web/
│
└── api/
    ├── src/
    │   ├── TechVault.Api/
    │   ├── TechVault.Application/
    │   ├── TechVault.Domain/
    │   └── TechVault.Infrastructure/
    │
    └── tests/
        ├── TechVault.UnitTests/
        └── TechVault.IntegrationTests/

infra/
├── nginx/
├── docker/
└── scripts/

docs/
├── architecture/
├── api/
└── decisions/

docker-compose.yml
README.md
.env.example
```

---

# 79. Docker

Services:

```txt
frontend
api
postgres
redis
minio
nginx
```

Production exposes only:

```txt
80
443
```

---

# 80. Environment Configuration

```txt
DATABASE_URL
REDIS_URL

ADMIN_AUTH_SECRET

S3_ENDPOINT
S3_ACCESS_KEY
S3_SECRET_KEY
S3_BUCKET

NEXT_PUBLIC_API_URL
```

Never commit secrets.

---

# 81. Nginx

```txt
https://techvault.example
        │
        ├── /api/* → ASP.NET Core
        │
        └── /*     → Next.js
```

Configure:

```txt
reverse proxy
compression
security headers
proxy headers
upload limits
HTTP/2
```

---

# 82. TLS

Use:

```txt
Let's Encrypt
```

Requirements:

```txt
HTTP → HTTPS
Automatic renewal
TLS 1.2+
```

---

# 83. CI/CD

GitHub Actions.

PR:

```txt
Frontend lint
Frontend type-check
Frontend tests
Frontend build

.NET restore
.NET build
.NET tests
```

Main:

```txt
Test
 ↓
Build images
 ↓
Push registry
 ↓
Deploy VPS
 ↓
Run migrations
 ↓
Restart
 ↓
Health check
```

---

# 84. Database Migration

Use:

```txt
EF Core Migrations
```

Do not silently auto-migrate production DB during normal app startup.

---

# 85. Backup

PostgreSQL:

```txt
Daily backup
```

Retention:

```txt
7 daily
4 weekly
```

Backup restore must eventually be tested.

---

# 86. Performance Targets

```txt
LCP < 2.5s
CLS < 0.1
INP < 200ms
```

Images:

```txt
AVIF / WebP
Responsive
Lazy-loaded
Correct dimensions
```

Do not preload 3D.

---

# 87. Accessibility

Required:

```txt
Semantic HTML
Keyboard navigation
Focus states
Alt text
Contrast
Accessible forms
```

---

# 88. Responsive Design

Compare on mobile:

```txt
sticky device headers
horizontal scroll when necessary
```

Timeline:

```txt
desktop → horizontal
mobile  → vertical
```

---

# 89. Design System

Palette:

```txt
Background
#0B0B0C

Surface
#151517

Primary text
#F5F5F5

Secondary text
#A0A0A0
```

Typography:

```txt
Headings
Geist / Space Grotesk

Body
Inter / Geist

Specs
JetBrains Mono
```

---

# 90. Core Frontend Components

```txt
DeviceCard
BrandCard
SpecTable
SpecRow
Timeline
TimelineItem
CompareTable
SearchDialog
Gallery
MediaViewer
ModelViewer
Breadcrumb
FilterPanel
Pagination
CollectionCard
EraSelector
```

---

# 91. Initial Seed Data

Start with approximately:

```txt
20 devices
```

Phones:

```txt
Nokia 3210
Nokia 3310
Nokia 6600
Nokia N95
Motorola Razr V3
BlackBerry Bold 9000
Original iPhone
iPhone 4
Samsung Galaxy S
HTC Dream
```

Computers:

```txt
Apple II
Commodore 64
IBM PC 5150
Macintosh 128K
NeXT Computer
ThinkPad 700C
iMac G3
Power Mac G4
MacBook Pro 2006
MacBook Air 2008
```

---

# 92. MVP MUST HAVE

```txt
Homepage

Phones
Computers

Device listing
Device detail
Specs

Brands
Categories

Search

Compare two devices

Global timeline
Phone timeline
Computer timeline

Admin CRUD

Image upload

PostgreSQL
Redis

SEO metadata
Canonical
Sitemap
Robots
Structured data

Docker
Nginx
HTTPS
VPS deployment

CI/CD

Logging
Health checks
Backup
```

---

# 93. MVP SHOULD HAVE

```txt
Museum landing page
Collections
Historical stories
Basic analytics
Basic background jobs
3D support for selected devices
```

---

# 94. NOT MVP

```txt
User accounts
Favorites
Comments
Reviews
Community submissions

Game consoles
Audio devices
Cameras

AI features

Elasticsearch

Microservices
Kafka
Kubernetes

Mobile app
```

---

# 95. Development Order

```txt
CONTENT MODEL
      ↓
DEVICE PAGES
      ↓
SEO
      ↓
SEARCH
      ↓
COMPARE
      ↓
TIMELINE
      ↓
MUSEUM UX
      ↓
DEPLOYMENT
      ↓
TRAFFIC
```

---

# 96. Phase 1 — Foundation

Build:

```txt
Next.js
ASP.NET API
PostgreSQL
Redis
Docker Compose
EF Core
Health endpoints
```

Architecture foundation:

```txt
Api
Application
Domain
Infrastructure
```

Create first Vertical Slice:

```txt
GetDevice
```

Acceptance:

```txt
Frontend calls API.
API talks to PostgreSQL.
Redis connection works.
Entire stack starts with Docker Compose.
```

---

# 97. Phase 2 — First Vertical Slice

First device:

```txt
Nokia 3310
```

Build:

```txt
Database
   ↓
.NET API
   ↓
GetDeviceHandler
   ↓
Admin CMS
   ↓
Image upload
   ↓
Next.js
   ↓
Specs
   ↓
SEO
```

Acceptance:

```txt
/devices/nokia-3310
```

works from real database data.

---

# 98. Phase 3 — Phone Comparison

Add:

```txt
Nokia 3210
```

Implement:

```txt
CompareDevicesQuery
CompareDevicesHandler
PhoneComparisonStrategy
```

Acceptance:

```txt
/compare/nokia-3310-vs-nokia-3210
```

works and is server-rendered.

---

# 99. Phase 4 — Computer Vertical Slice

Add:

```txt
Macintosh 128K
iMac G3
```

Create:

```txt
DesktopComparisonStrategy
```

Verify shared code has no phone-specific assumptions.

Architecture is considered valid only when:

```txt
Phone
Computer
```

both work naturally.

---

# 100. Phase 5 — Browse & Search

Implement:

```txt
/phones
/computers
/devices
/brands
```

Use:

```txt
DeviceSearchQuery
DeviceQueryBuilder
```

for filtering/sorting.

---

# 101. Phase 6 — Timeline

Implement:

```txt
/timeline
/timeline/phones
/timeline/computers
/timeline/nokia
/timeline/apple
```

---

# 102. Phase 7 — Museum

Implement:

```txt
/museum
Iconic devices
Explore by era
Collections
Historical stories
```

Do not polish museum before core data architecture is stable.

---

# 103. Phase 8 — SEO

Implement:

```txt
metadata
canonical
structured data
sitemap
robots
breadcrumbs
OpenGraph
internal linking
```

---

# 104. Phase 9 — Production Deployment

```txt
Domain
  ↓
DNS
  ↓
Cloudflare
  ↓
Nginx
  ↓
Docker
  ├── Next.js
  ├── .NET API
  ├── PostgreSQL
  ├── Redis
  └── MinIO
```

---

# 105. Phase 10 — CI/CD

```txt
git push
   ↓
GitHub Actions
   ↓
Tests
   ↓
Build Docker
   ↓
Push images
   ↓
Deploy VPS
   ↓
Migration
   ↓
Health check
```

---

# 106. Definition of Done

Feature complete when:

```txt
Works desktop
Works mobile

Loading state
Empty state
Error state

Backend validation

No console errors

SEO considered

Critical logic tested

Logging where useful

No obvious accessibility issue
```

Backend feature also requires:

```txt
Business logic not in controller

No unnecessary repository wrapper

Expected errors use Result Pattern

Feature organized as Vertical Slice

Queries use AsNoTracking where appropriate

CancellationToken propagated

Database calls async

No Infrastructure dependency inside Domain
```

---

# 107. Coding Rules

Prefer:

```txt
Simple code
Explicit naming
Strong typing
Small modules
Vertical slices
Clear API contracts
Composition over inheritance
```

Avoid:

```txt
unnecessary abstractions
generic base classes
deep inheritance
service classes with dozens of methods
repository wrappers over EF Core
premature distributed architecture
```

Rule:

> Do not introduce a design pattern unless it solves an actual problem in TechVault.

---

# 108. Testing Strategy

Unit tests focus on:

```txt
Domain rules
Comparison strategies
Specification value parsing
Query/filter logic where useful
```

Integration tests focus on:

```txt
EF Core queries
PostgreSQL behavior
API endpoints
Admin writes
Search
Comparison
```

Avoid mocking EF Core extensively.

Prefer integration tests against a real PostgreSQL test container where practical.

---

# 109. Initial Agent Task

When using Codex / Claude Code:

```txt
1. Create monorepo.

2. Scaffold Next.js.

3. Scaffold ASP.NET Core.

4. Create:
   - TechVault.Api
   - TechVault.Application
   - TechVault.Domain
   - TechVault.Infrastructure

5. Configure project references according to Clean Architecture boundaries.

6. Configure PostgreSQL.

7. Configure Redis.

8. Configure Docker Compose.

9. Implement ITechVaultDbContext.

10. Implement TechVaultDbContext.

11. Implement Result<T>.

12. Implement Brand.

13. Implement Category.

14. Implement ComparisonGroup.

15. Implement Device.

16. Implement SpecificationGroup.

17. Implement SpecificationDefinition.

18. Implement DeviceSpecification.

19. Create EF Core migrations.

20. Seed Nokia brand.

21. Seed Phones category.

22. Seed PHONE comparison group.

23. Seed Nokia 3310.

24. Create Devices/GetDevice vertical slice.

25. Implement GET /api/v1/devices/{slug}.

26. Build /devices/nokia-3310.

27. Render content server-side.

28. Add SEO metadata.

29. Verify Docker Compose environment.
```

Do not introduce:

```txt
MediatR
Generic Repository
Custom UnitOfWork
Domain Events
Authentication
3D
Collections
```

during this task.

---

# 110. Second Agent Task

After Nokia 3310:

```txt
1. Add Nokia 3210.

2. Add device relationships.

3. Create Comparisons/CompareDevices vertical slice.

4. Create IDeviceComparisonStrategy.

5. Implement PhoneComparisonStrategy.

6. Implement ComparisonStrategyResolver.

7. Build:

/compare/nokia-3310-vs-nokia-3210

8. Add show-differences mode.

9. Verify SSR.

10. Add internal links.
```

---

# 111. Third Agent Task

Validate Computers:

```txt
1. Add Computers category.

2. Add Desktop and Laptop.

3. Add DESKTOP and LAPTOP comparison groups.

4. Seed Macintosh 128K.

5. Seed iMac G3.

6. Add computer specification definitions.

7. Render computer pages using same Device model.

8. Implement DesktopComparisonStrategy.

9. Build computer timeline.

10. Verify shared code contains no phone-specific hacks.
```

---

# 112. Architectural Guardrails

The coding agent must not automatically introduce:

```txt
Repository<T>
UnitOfWork
MediatR
AutoMapper
MassTransit
Kafka
RabbitMQ
Microservices
Event Sourcing
Kubernetes
```

unless an explicit future requirement justifies them.

Prefer direct, readable code.

Example:

```txt
Controller
   ↓
Handler
   ↓
DbContext
```

is acceptable.

Do not turn it into:

```txt
Controller
 ↓
Mediator
 ↓
Handler
 ↓
Service
 ↓
Repository
 ↓
UnitOfWork
 ↓
DbContext
```

without concrete benefit.

---

# 113. Architectural Decision Summary

Final backend architecture:

```txt
Modular Monolith
        +
Clean Architecture boundaries
        +
Vertical Slice Architecture
        +
CQRS-lite
```

Primary patterns:

```txt
Strategy
Query Object
Result
Value Object
Cache Aside
Dependency Injection
Factory where justified
```

Persistence:

```txt
EF Core DbContext directly
```

No:

```txt
Generic Repository
Custom Unit of Work
```

MediatR:

```txt
Not in MVP
```

Domain Events:

```txt
Future, only when side effects justify them
```

---

# 114. Product Principle

Whenever choosing between:

```txt
more features
```

and:

```txt
better data
better architecture
better browsing
better SEO
better performance
better presentation
```

choose the second.

TechVault's main value:

```txt
accurate structured data
+
historical context
+
interactive comparisons
+
timeline exploration
+
beautiful visual presentation
```

---

# 115. Content Expansion Strategy

```txt
4 devices
 ↓
20
 ↓
50
 ↓
100
 ↓
500+
```

Do not add hundreds of incomplete devices.

---

# 116. Initial Milestone

First prove:

```txt
Nokia 3310
     │
     ├── Specs
     ├── Images
     ├── History
     └── SEO
          │
          ▼
Nokia 3210
     │
     ▼
PhoneComparisonStrategy
     │
     ▼
Compare Page
     │
     ▼
Phone Timeline
```

Then:

```txt
Macintosh 128K
      │
      ▼
iMac G3
      │
      ▼
DesktopComparisonStrategy
      │
      ▼
Computer Compare
      │
      ▼
Computer Timeline
```

At that point both product architecture and backend architecture are validated.

---

# 117. Final Vision

TechVault should allow someone searching:

```txt
Nokia 3310 specs
```

or:

```txt
Macintosh 128K specifications
```

to find a high-quality technical answer and then naturally explore the historical context around that device.

Core loop:

```txt
Google Search
     ↓
Device Specs
     ↓
Compare
     ↓
Brand / Family
     ↓
Timeline
     ↓
Museum
     ↓
More Devices
```

Backend principle:

```txt
Simple enough to understand
Structured enough to scale
```

The system should demonstrate good .NET architecture without becoming an architecture-demo project whose abstractions are more complicated than its business domain.
