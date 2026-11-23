# Notas de release / Release notes

Toda versão publicada (mesmo de testes) tem as notas em **`docs/releases/<tag>.md`**, escritas **em português e em
inglês**, e **bem explicadas**: o que mudou, como atualizar, o que ainda não funciona e como conferir o arquivo.
O workflow `preview-release.yml` **não cria a release** se esse arquivo não existir ou não tiver as duas línguas;
o texto do arquivo vira a descrição da release no GitHub.

Every published version (even a preview) has its notes in **`docs/releases/<tag>.md`**, written **in Portuguese and
in English**, and **well explained**: what changed, how to update, what still does not work, and how to check the
file. The `preview-release.yml` workflow **refuses to create the release** if that file is missing or lacks either
language; the file's text becomes the release description on GitHub.

Como escrever / How to write: copie `_modelo.md`, troque o nome pela tag (por exemplo `v0.1.0-preview.3.md`) e
preencha as duas partes com o mesmo conteúdo. Liste o que o usuário vai **ver ou notar**, não só nomes de PR.
Copy `_modelo.md`, rename it to the tag, and fill in both parts with the same content. List what the user will
**see or notice**, not just PR names.
