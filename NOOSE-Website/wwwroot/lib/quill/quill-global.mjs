// ESM-Shim fuer das vendored table-module.js: liefert das selbst gehostete UMD-Quill
// (window.Quill) als Default-Export. Dadurch loest der modul-interne Import
// (urspruenglich `import Quill from "quill"`) im Browser ohne Import-Map auf.
// WICHTIG: window.Quill wird von richtext.js (ladeQuill) gesetzt, BEVOR das Tabellen-Modul
// importiert wird – daher steht es hier zur Auswertungszeit bereits zur Verfuegung.
export default window.Quill;
