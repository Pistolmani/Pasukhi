export type ManagedAdminUser = {
  id: string
  email: string
  firstName: string
  lastName: string
  role: string
  businessId: string | null
  businessName: string | null
  createdAt: string
}

export type CreateAdminUserRequest = {
  email: string
  firstName: string
  lastName: string
  password: string
  businessId: string
}
