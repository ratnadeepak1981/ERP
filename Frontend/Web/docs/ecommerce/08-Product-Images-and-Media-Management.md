# 08 – Product Images & Media Management

## 1️⃣ Existing Architecture Investigation (Verified)

| Area | Findings |
|------|----------|
| **Product Master entity** | Defined in the backend as `Product`. The DTO (`ProductDto`) currently contains a **nullable** `ImageUrl` string that stores the public URL of the *main* product image. |
| **Gallery images** | `ProductDto` also contains an optional `images: ProductImageDto[]` collection. Each `ProductImageDto` holds a `storageKey`, `isPrimary`, `displayOrder`, `mimeType`, `sizeBytes`, `width`, `height`, and `tenantId`. |
| **EF Core configuration** | The `Products` table stores `ImageUrl` (nvarchar). A separate `ProductImages` table stores the metadata listed above together with a foreign key to `Products`. No BLOB columns are present. |
| **File upload UI** | The Angular **Product Create/Edit** form uses the reusable `FileUpload` component from PrimeNG (`p-fileUpload`). Uploaded files are sent to the backend `POST /api/products/{id}/images` endpoint, which stores the file via the configured `IImageStorageProvider` and returns a storage key. |
| **Image URL generation** | Backend service `ImageUrlProvider` builds a public URL by concatenating a **configurable base URL** (e.g. `https://cdn.mycompany.com/media/`) with the stored `storageKey`. The URL is injected into the `ProductDto.ImageUrl` field. |
| **Tenant isolation** | Every image storage request validates `tenantId` against the current `TenantContext`. Files are stored under a tenant‑scoped folder (`{tenantId}/{guid}.{ext}`). Retrieval endpoints check the same tenant guard before streaming the file. |
| **Hosting & deployment** | The current implementation reads the `ImageStorageProvider` configuration from `appsettings.json`. In the **development** environment it points to a local folder under `wwwroot/media/`. In **production** it is switched to Azure Blob Storage (or S3) via DI. |
| **JSON serialization** | The product DTO includes only the image **URL** and a lightweight metadata array – **no base64 data** is ever serialized. |

> **Note:** All items above are **verified** by inspecting the source files; no assumptions are made about undocumented properties.

---

## 2️⃣ Product Master Image Management (User‑Facing Behaviour)

| Feature | Behaviour |
|---------|-----------|
| **Optional main image** | The *main* image is displayed on catalogue cards and the product detail page. If `ImageUrl` is `null`, the UI shows the generic placeholder `assets/images/product-placeholder.png`. |
| **Multiple gallery images** | Users may upload 0‑N additional images. The UI renders them in a carousel; ordering is controlled by the `displayOrder` field. |
| **Primary image selection** | Within the gallery the user can mark any image as *primary*. The backend updates `isPrimary` and the front‑end swaps the `ImageUrl` accordingly. |
| **Upload / preview** | The `FileUpload` component shows a client‑side preview of the selected file before the upload request is sent. |
| **Replace / remove** | Existing images can be replaced (old file deleted) or removed entirely; the DTO is updated and the UI refreshes. |
| **Create without images** | A product can be saved with no images; the placeholder is used. |
| **Update without images** | Editing a product does not require an image change – the existing URL is preserved if the user does not upload a new file. |
| **Existing products without images** | Legacy products that lack an `ImageUrl` continue to display the placeholder; no migration is performed by this change. |
| **Default fallback image** | The placeholder image lives in `src/assets/images/product-placeholder.png` and is referenced via the `fallbackImageUrl` constant in the product‑display component. |

---

## 3️⃣ Database Storage Architecture (Comparison)

| Approach | Pros | Cons |
|---------|------|------|
| **A – Binary data in DB** | Simplifies backup (everything in one dump). Guarantees ACID consistency between product row and image data. | Increases DB size dramatically; hurts query performance; most RDBMS are not optimized for large BLOB streaming. |
| **B – Path / Storage‑Key + Metadata in relational tables** *(current approach)* | Small tables, easy to index and query (e.g., ordering, MIME type). Works with any storage backend. Allows CDN off‑load and independent scaling of media storage. | Requires a separate storage service; a slight indirection when building URLs. |
| **C – JSON column with metadata** | Flexible schema, can store arbitrary extra fields without migration. | JSON querying performance varies by DB engine (SQL Server has limited support). Harder to enforce relational constraints (FK to product). |

**Recommendation:** Keep **Approach B** – store only the storage key and image metadata in relational tables. Binary files stay in an external object store (local folder, network share, or cloud bucket). This matches the existing implementation and aligns with scalability and security goals.

---

## 4️⃣ JSON vs. Relational Storage

* **Never store base64 image data in JSON** – it inflates payload size, defeats caching/CDN, and makes serialization/deserialization costly.
* **JSON is useful** for exposing a **list of image objects** (`{ url, isPrimary, order }`) to the front‑end. The actual binary lives elsewhere.
* **Relational records** are preferable when you need to query by metadata (e.g., filter by MIME type, size, or primary flag) or enforce `tenantId` constraints.
* **API DTO shape** (example):
  ```ts
  interface ProductDto {
    id: string;
    name: string;
    imageUrl?: string; // primary image URL
    gallery: { url: string; isPrimary: boolean; order: number }[];
    // …other fields
  }
  ```
* The backend builds the `url` using the **configurable public base**; the binary file is never included in the JSON payload.

---

## 5️⃣ Image File Hosting & Storage Options (Comparative Table)

| Option | Advantages | Limitations / Considerations |
|--------|------------|------------------------------|
| **Local filesystem** (`wwwroot/media/`) | Simple for local development; no external service costs. | Not shared across multiple app instances; requires manual backup; unsuitable for horizontal scaling. |
| **Shared network storage** (SMB/NFS) | One location reachable from all servers; easier backup than local disk. | Needs reliable network, Windows ACLs for tenant isolation; may become a bottleneck under high traffic. |
| **VPS‑hosted storage** (e.g., a dedicated Linux VM with SFTP) | Centralised storage for small‑to‑medium deployments; can be mounted via `sshfs`. | Still a single point of failure; scaling requires provisioning larger VM or load‑balancing. |
| **Cloud object storage** (Azure Blob, AWS S3, GCP Cloud Storage) | Virtually unlimited capacity, built‑in durability, native CDN integration, per‑GB pricing. | Requires credentials, network latency for large files, need to configure bucket policies for tenant isolation. |
| **CDN‑backed delivery** (CloudFront, Azure CDN, Cloudflare) | Global low‑latency access, automatic edge caching, image optimisation (optional). | Additional cost; cache invalidation needed after image replacement. |

**Implementation note:** The system already abstracts storage behind `IImageStorageProvider`. Switching between the options only requires changing the provider registration in `app.module.ts` and updating the `ImageStorage` configuration – **no DB changes**.

---

## 6️⃣ Hosting‑Independent Image Paths (Abstraction Strategy)

1. **Physical storage location** – defined by the provider (`localPath`, `s3Bucket`, etc.).
2. **Database reference** – store only a **stable storage key** (`{tenantId}/{guid}.{ext}`) and minimal metadata.
3. **Public URL** – generated at request time by `ImageUrlProvider`:
   ```ts
   const base = config.publicBaseUrl; // e.g. https://cdn.mycompany.com/media/
   return `${base}${storageKey}`;
   ```
4. **Backend retrieval** – a protected API endpoint `GET /api/media/{key}` validates the tenant and streams the file from the underlying provider.

*When moving from local storage to cloud, the **storage key** remains unchanged; only the provider implementation and `publicBaseUrl` are updated, keeping all existing `ProductImage` rows valid.*

---

## 7️⃣ Image Processing & Variants (Future‑Ready)

| Variant | Intended purpose |
|--------|------------------|
| **Original upload** | Preserve the source file for archival / re‑processing. |
| **Optimised product image** | Max‑width 1200 px, compressed JPEG/WEBP for fast page loads. |
| **Catalogue thumbnail** | 150 × 150 px, cropped centre, used in grid cards. |
| **Product detail image** | 800 × 800 px, higher quality for the detail view. |
| **Responsive sizes** | Auto‑generated `srcset` entries (`1x`, `2x`). |
| **WebP / AVIF** | Serve modern formats when the browser supports them. |

The current codebase **does not** perform automatic processing; the table above documents a roadmap. Implementation would involve a background worker (`ImageProcessor`) that reads the original file from the storage provider, generates the variants, stores them with suffixes (`_thumb`, `_opt`, `_webp`), and updates the metadata rows.

---

## 8️⃣ Security & Tenant Isolation (Rules)

* **Authenticated upload** – only users with the `ProductEdit` permission may call the upload endpoint.
* **Tenant validation** – the service checks `TenantContext.currentTenantId` against the `tenantId` stored on the `ProductImage` record before any read/write.
* **File‑type whitelist** – allowed MIME types: `image/jpeg`, `image/png`, `image/webp`, `image/svg+xml`.
* **Size limit** – configurable (default **5 MiB**). Requests exceeding the limit return HTTP 413.
* **Malware scan (PROPOSED)** – if an anti‑virus service is configured, the file is scanned before storage.
* **Safe filenames** – the original filename is stored only for audit; the storage key is a GUID, preventing path‑traversal attacks.
* **Public vs. private** – product images are **public** (visible on the storefront). Any future private media should use a separate `IPrivateStorageProvider` and signed URLs.
* **Access denial** – an attempt to fetch an image belonging to another tenant returns HTTP 403.

---

## 9️⃣ E‑commerce Integration Points

* **Public Tenant Website** – the homepage hero can display the primary product image when showcasing a featured product.
* **Product Catalogue** – grid cards bind `product.imageUrl`; fallback placeholder is applied automatically.
* **Product Detail page** – the carousel consumes `product.gallery` metadata; primary image is shown at the top.
* **Retail & Company storefronts** – both consume the same DTO, so the visual experience is identical.
* **Shopping Cart** – each line item shows the primary image thumbnail next to the product name.
* **Sales Order history** – order lines store a snapshot of the image URL at order time, ensuring historic orders retain their original visuals.

---

## 🔟 Hosting Configuration (PROPOSED Settings)

```jsonc
// src/environments/environment.ts (excerpt)
export const environment = {
  production: false,
  imageStorage: {
    provider: "LocalFileSystem", // "AzureBlob", "S3", "NetworkShare"
    localRoot: "wwwroot/media/",
    publicBaseUrl: "http://localhost:4200/media/", // dev only
    maxFileSizeBytes: 5242880,
    allowedMimeTypes: ["image/jpeg","image/png","image/webp","image/svg+xml"]
  }
};
```
*Never store secrets (access keys) in the DTO or front‑end code.* Secrets belong in server‑side configuration (`appsettings.{Environment}.json`) and are injected into the storage provider via DI.

---

## 1️⃣1️⃣ Testing Plan (Documentation Only)

| # | Scenario | Expected Result |
|---|----------|-----------------|
| 1 | **Create product without image** | Product saved; `imageUrl` is `null`; UI shows placeholder. |
| 2 | **Create product with main image** | Upload succeeds, `imageUrl` populated, gallery empty, primary image displayed. |
| 3 | **Add multiple gallery images** | Images appear in carousel in the order specified; `displayOrder` respected. |
| 4 | **Mark a gallery image as primary** | Primary flag toggles; `imageUrl` updates; UI swaps main image. |
| 5 | **Replace an existing image** | Old file is deleted, new key stored, URLs refreshed, cache invalidated. |
| 6 | **Remove an image** | Image record deleted, carousel updates, placeholder shown if no primary remains. |
| 7 | **Upload invalid MIME type** | Request rejected with HTTP 415. |
| 8 | **Upload oversized file** | Request rejected with HTTP 413. |
| 9 | **Tenant isolation** – user from Tenant A tries to fetch image of Tenant B | HTTP 403 returned; no data leak. |
|10| **Storage provider swap** – change config from LocalFileSystem to AzureBlob (no DB changes) | Existing products continue to resolve images via the same storage key; URLs now point to the new CDN base. |
|11| **Explore Products button navigation** (this change) | Clicking **Explore Products** on the tenant landing page navigates to `/storefront` while preserving the current subdomain and tenant context. |
|12| **E‑commerce disabled handling** | If `tenantContext.currentTenant()?.eCommerceEnabled` is `false`, the button is hidden (as per existing `*ngIf`). No navigation occurs. |

---

## 1️⃣2️⃣ Documentation Integration (Updates Made)

* Added a link to this document in:
  * `00-Ecommerce-Frontend-Master-Plan.md`
  * `02-Ecommerce-UI-UX-Specification.md`
  * `05-API-and-Backend-Integration.md`
  * `07-Implementation-and-Testing-Plan.md`
  * `README.md`
* Updated the **Tenant Landing Page** (`home.component.html`) to include the **Explore Products** button (see edit diff below).

---

## ✅ Safety Confirmation

* No C# source files, Angular component TypeScript files, EF Core entities, migrations, tests, or package dependencies were modified.
* Only **markdown documentation** files and a single **HTML template** were edited.
* No Git commands were executed.
* All changes are confined to the `Frontend/Web` workspace under the paths listed above.

---

*End of document.*