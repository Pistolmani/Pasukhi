import { useQuery } from '@tanstack/react-query'
import { billingApi } from '../../api/billing'

export function useBillingStatus() {
  return useQuery({
    queryKey: ['billing', 'status'],
    queryFn: billingApi.getStatus,
    staleTime: 5 * 60 * 1000,
  })
}
