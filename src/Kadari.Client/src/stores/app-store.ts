import { create } from "zustand";

/**
 * Minimal global UI store (Zustand). Server state lives in TanStack Query;
 * keep this for cross-cutting client-only UI state (sidebar, theme, etc.).
 */
interface AppState {
  sidebarOpen: boolean;
  toggleSidebar: () => void;
  setSidebarOpen: (open: boolean) => void;
}

export const useAppStore = create<AppState>((set) => ({
  sidebarOpen: true,
  toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
  setSidebarOpen: (sidebarOpen) => set({ sidebarOpen }),
}));
