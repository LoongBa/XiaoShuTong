import { TanStackRouterVite } from "@tanstack/router-plugin/vite";
import tailwindcss from "@tailwindcss/vite";
import viteReact from "@vitejs/plugin-react";
import { defineConfig, type Plugin } from "vite";
import tsConfigPaths from "vite-tsconfig-paths";
import { existsSync } from "fs";
import { resolve } from "path";
import { APP_NAME } from "./src/config/app";

const SOURCE_LOCATION_PLUGIN_CANDIDATES = [
  process.env.MEOO_SOURCE_LOCATION_PLUGIN_PATH,
  "/app/sdk/lib/src/plugins/source-location-babel.js",
  resolve(process.cwd(), "node_modules/@ali/oneday-agent-sdk/lib/src/plugins/source-location-babel.js"),
].filter(Boolean) as string[];

const SOURCE_LOCATION_PLUGIN_PATH = SOURCE_LOCATION_PLUGIN_CANDIDATES.find((path) => existsSync(path));

/** 构建时用公共配置常量注入 <title>（保持 index.html 与页面标题一致） */
function injectAppTitle(title: string): Plugin {
  return {
    name: "inject-app-title",
    transformIndexHtml(html) {
      return html.replace(/<title>.*?<\/title>/, `<title>${title}</title>`);
    },
  };
}

/**
 * React + Vite 构建配置
 *
 * 硬约束：
 * - dev server 必须监听 3015 + strictPort（沙箱只开放一个代理端口）
 * - outDir 'dist' / assetsDir 'assets' — 归一化产物目录
 */
export default defineConfig({
  plugins: [
    tailwindcss(),
    TanStackRouterVite(),
    viteReact({
      babel: {
        plugins: SOURCE_LOCATION_PLUGIN_PATH
          ? [[SOURCE_LOCATION_PLUGIN_PATH, { projectRoot: process.cwd() }]]
          : [],
      },
    }),
    tsConfigPaths(),
    injectAppTitle(APP_NAME),
  ],
  server: {
    host: "0.0.0.0",
    port: 3015,
    strictPort: true,
    allowedHosts: true,
    // HMR 默认关闭：沙箱预览 iframe 下 HMR 的整页 reload 会放大任何 transform error
    // 如需热更，改为: hmr: { clientPort: 443, protocol: 'wss' }
    hmr: false,
    proxy: {
      "/graphql": {
        target: "http://localhost:5157",
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: "dist",
    assetsDir: "assets",
    emptyOutDir: true,
    rollupOptions: {
      output: {
        entryFileNames: "assets/[name]-[hash].js",
        chunkFileNames: "assets/[name]-[hash].js",
        assetFileNames: "assets/[name]-[hash][extname]",
      },
    },
  },
  optimizeDeps: {
    exclude: ["@tkwf/tsclient", "@tkwf/tsclient-mock"],
  },
});
