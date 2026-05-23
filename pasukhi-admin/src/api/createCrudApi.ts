import api from './client'

export function createCrudApi<TItem, TCreate = unknown, TUpdate = TCreate>(basePath: string) {
  return {
    list: (): Promise<TItem[]> =>
      api.get<TItem[]>(basePath).then((r) => r.data),
    getById: (id: string): Promise<TItem> =>
      api.get<TItem>(`${basePath}/${id}`).then((r) => r.data),
    create: (data: TCreate): Promise<TItem> =>
      api.post<TItem>(basePath, data).then((r) => r.data),
    update: (id: string, data: TUpdate): Promise<TItem> =>
      api.put<TItem>(`${basePath}/${id}`, data).then((r) => r.data),
    remove: (id: string): Promise<void> =>
      api.delete(`${basePath}/${id}`).then(() => undefined),
  }
}
