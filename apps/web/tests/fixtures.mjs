// Synthetic contract fixtures for frontend tests only; never used by application routes.
export const brand = {
  id: "11111111-1111-4111-8111-111111111111",
  name: "Nokia",
  slug: "nokia",
  description: "Test brand",
  createdAt: "2026-09-14T08:00:00+00:00",
  updatedAt: "2026-09-14T08:00:00+00:00",
  publishedDeviceCount: 2,
};
export const category = {
  id: "22222222-2222-4222-8222-222222222222",
  name: "Feature Phones",
  slug: "feature-phones",
  parentCategoryId: "33333333-3333-4333-8333-333333333333",
  parentSlug: "phones",
  description: "Test category",
  displayOrder: 10,
};
export const specification = {
  id: "66666666-6666-4666-8666-666666666666",
  key: "test_number",
  name: "Test precision",
  dataType: "number",
  unit: "mm",
  displayOrder: 10,
  valueText: null,
  valueNumber: 0.125,
  valueBoolean: null,
  valueDate: null,
};
export const card = {
  id: "44444444-4444-4444-8444-444444444444",
  name: "Nokia 3310",
  slug: "nokia-3310",
  shortDescription: "Synthetic frontend test record.",
  brand,
  category,
  releaseYear: 2000,
  releaseDate: null,
};
export const detail = {
  ...card,
  description: "Test overview, rendered on the server.",
  history: "Test history, rendered on the server.",
  seoTitle: "Nokia 3310 specifications and history | TechVault",
  seoDescription: "Test SEO description.",
  discontinuedDate: null,
  physicalDetails: { heightMm: null, widthMm: null, depthMm: null, weightGrams: 133 },
  createdAt: brand.createdAt,
  updatedAt: brand.updatedAt,
  publishedAt: brand.createdAt,
  specificationGroups: [{
    id: "55555555-5555-4555-8555-555555555555",
    key: "test_group",
    name: "Test group",
    displayOrder: 10,
    specifications: [
      specification,
      { ...specification, id: "77777777-7777-4777-8777-777777777777", key: "test_boolean", name: "Test disabled", dataType: "boolean", valueNumber: null, valueBoolean: false, unit: null },
      { ...specification, id: "88888888-8888-4888-8888-888888888888", key: "test_zero", name: "Test zero", valueNumber: 0 },
    ],
  }],
};
export function paged(data, page = 1, pageSize = 12, total = data.length) {
  return { data, pagination: { page, pageSize, total, totalPages: Math.ceil(total / pageSize) } };
}

export const appleBrand = { ...brand, id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", name: "Apple", slug: "apple" };
export const computerCategory = {
  ...category,
  id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
  name: "All-in-one Computers",
  slug: "all-in-one-computers",
  parentCategoryId: "cccccccc-cccc-4ccc-8ccc-cccccccccccc",
  parentSlug: "computers",
};
// Ordered as a synthetic relevance result, deliberately not chronological/alphabetical.
export const discoveryDevices = [
  card,
  { ...card, id: "dddddddd-dddd-4ddd-8ddd-dddddddddddd", name: "Nokia 3210", slug: "nokia-3210", releaseYear: 1999 },
  { ...card, id: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee", name: "iMac G3", slug: "imac-g3", brand: appleBrand, category: computerCategory, releaseYear: 1998, releaseDate: "1998-08-15" },
  { ...card, id: "ffffffff-ffff-4fff-8fff-ffffffffffff", name: "Macintosh 128K", slug: "macintosh-128k", brand: appleBrand, category: computerCategory, releaseYear: 1984, releaseDate: "1984-01-24" },
];
