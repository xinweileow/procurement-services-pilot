/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Inter', 'system-ui', 'sans-serif'],
      },
      fontSize: {
        '2xs': '0.6875rem',
      },
      colors: {
        canvas: { DEFAULT: '#F9F9F9', subtle: '#F1F4F8' },
        surface: { DEFAULT: '#FFFFFF', 2: '#FAFBFC', raised: '#F5F7FA' },
        ink: {
          DEFAULT: '#0F172A',
          muted: '#64748B',
          subtle: '#94A3B8',
          inverse: '#0F172A',
        },
        line: { DEFAULT: '#E2E8F0', strong: '#CBD5E1' },
        accent: { DEFAULT: '#FFD200', hover: '#F5C400', soft: '#FFF6CC' },
        info: { DEFAULT: '#2563EB', soft: '#DBEAFE' },
        success: { DEFAULT: '#16A34A', soft: '#DCFCE7' },
        warning: { DEFAULT: '#F59E0B', soft: '#FEF3C7' },
        danger: { DEFAULT: '#DC2626', soft: '#FEE2E2' },
        sidebar: { text: '#CBD5E1', muted: '#7C8AA3' },
      },
      boxShadow: {
        xs: '0 1px 2px 0 rgb(15 23 42 / 0.04)',
        ring: '0 0 0 3px rgb(255 210 0 / 0.35)',
      },
    },
  },
  plugins: [],
}
