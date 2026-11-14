// Sidebar list primitives adapted from the shadcn/ui New York v4 registry (MIT).
// https://ui.shadcn.com/r/styles/new-york-v4/sidebar.json
export function SidebarMenu({ children }) {
  return <ul data-slot="sidebar-menu" data-sidebar="menu">{children}</ul>;
}

export function SidebarMenuItem({ children }) {
  return <li data-slot="sidebar-menu-item" data-sidebar="menu-item">{children}</li>;
}
