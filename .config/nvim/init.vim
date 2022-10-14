" __     ___                    
" \ \   / (_)_ __ ___  _ __ ___ 
"  \ \ / /| | '_ ` _ \| '__/ __|
"   \ V / | | | | | | | | | (__ 
"    \_/  |_|_| |_| |_|_|  \___|
"                              

syntax on
filetype plugin indent on
set nocompatible
set relativenumber
set number
set encoding=utf-8
set showcmd

set shiftwidth=4
set expandtab
set backspace=indent,eol,start

set hlsearch
set incsearch
set ignorecase
set smartcase

set termguicolors

call plug#begin()
Plug 'kaicataldo/material.vim', { 'branch': 'main' }
call plug#end()

let g:material_terminal_italics = 1
let g:material_theme_style = 'palenight'
colorscheme material
