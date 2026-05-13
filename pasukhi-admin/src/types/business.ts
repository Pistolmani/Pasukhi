export type Business = {
  id: string
  name: string
  slug: string
  description: string | null
  logoUrl: string | null
  isActive: boolean
  createdAt: string
}

export type CreateBusinessRequest = {
  name: string
  slug: string
  description: string | null
  logoUrl: string | null
}
