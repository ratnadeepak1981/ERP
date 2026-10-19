import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';
import { routes } from './app.routes';
import { authInterceptor } from './core/intercepters/auth.interceptor';

// Clean Enterprise Theme Preset extending PrimeNG Aura
export const CleanEnterprisePreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#f0f7fd',
      100: '#e0effb',
      200: '#badaf6',
      300: '#80bff1',
      400: '#3d9deb',
      500: '#0f6cbd', // Clean Enterprise Primary Blue
      600: '#115ea3', // Primary Hover
      700: '#0f548c',
      800: '#0c3b5e', // Primary Active
      900: '#0b3352',
      950: '#071f32'
    },
    colorScheme: {
      light: {
        surface: {
          0: '#ffffff',
          50: '#f8fafc',
          100: '#f1f5f9',
          200: '#e2e8f0',
          300: '#cbd5e1',
          400: '#94a3b8',
          500: '#64748b',
          600: '#475569',
          700: '#334155',
          800: '#1e293b',
          900: '#0f172a',
          950: '#020617'
        }
      },
      dark: {
        surface: {
          0: '#ffffff',
          50: '#020617',
          100: '#0f172a',
          200: '#1e293b',
          300: '#334155',
          400: '#475569',
          500: '#64748b',
          600: '#94a3b8',
          700: '#cbd5e1',
          800: '#e2e8f0',
          900: '#f1f5f9',
          950: '#f8fafc'
        }
      }
    }
  }
});

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes, withComponentInputBinding()),
    providePrimeNG({
      theme: {
        preset: CleanEnterprisePreset,
        options: {
          darkModeSelector: '.theme-fluent-dark',
          cssLayer: false
        }
      },
      ripple: false
    })
  ]
};
