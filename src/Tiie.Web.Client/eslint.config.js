import js from '@eslint/js';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'src/api/esquema.d.ts'] },
  {
    files: ['**/*.{ts,tsx}'],
    extends: [js.configs.recommended, ...tseslint.configs.strict, jsxA11y.flatConfigs.recommended],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'no-restricted-syntax': [
        'error',
        {
          // new Date('2026-11-12') es medianoche UTC: en Sinaloa es el día anterior. Usa lib/fechas.ts.
          selector: "NewExpression[callee.name='Date'][arguments.0.type='Literal']",
          message: 'No construyas fechas desde texto: usa las funciones de src/lib/fechas.ts.',
        },
        {
          selector: "NewExpression[callee.name='Date'][arguments.0.type='TemplateLiteral']",
          message: 'No construyas fechas desde texto: usa las funciones de src/lib/fechas.ts.',
        },
        {
          selector: "JSXAttribute[name.name='dangerouslySetInnerHTML']",
          message: 'Prohibido dangerouslySetInnerHTML: los textos se muestran como texto plano.',
        },
      ],
      '@typescript-eslint/consistent-type-imports': 'error',
    },
  },
  {
    files: ['*.config.{js,ts}'],
    languageOptions: { globals: globals.node },
  },
);
