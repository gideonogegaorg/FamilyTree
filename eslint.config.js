import js from "@eslint/js";
import globals from "globals";

export default [
  js.configs.recommended,
  {
    files: ["src/GMO.FamilyTree.Web/wwwroot/js/**/*.js"],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: "script",
      globals: {
        ...globals.browser,
        bootstrap: "readonly",
        jQuery: "readonly",
        $: "readonly",
        PhotoUpload: "readonly",
      },
    },
    rules: {
      "no-unused-vars": ["warn", { argsIgnorePattern: "^_" }],
      "no-undef": "error",
      "no-redeclare": "off",
    },
  },
];
