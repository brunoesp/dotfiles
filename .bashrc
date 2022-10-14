#
# ~/.bashrc
#

# If not running interactively, don't do anything
[[ $- != *i* ]] && return

alias ls='ls --color=auto'
alias lsd='exa -l'
alias lsa='exa -la'
alias vim='nvim'

function cs () {
    clear && cd "$@" && lsd
}

export PS1="\[\033[38;5;9m\]┌─[\[\033[38;5;221m\]\w\[\033[38;5;9m\]]\n\[\033[38;5;9m\]└──[\[\033[38;5;42m\]\u\[$(tput sgr0)\]\[\033[38;5;9m\]@\[\033[38;5;9m\]\[\033[38;5;39m\]\h\[$(tput sgr0)\]\[$(tput sgr0)\]\[\033[38;5;9m\]]\[\033[38;5;221m\]\\$\[$(tput sgr0)\] \[$(tput sgr0)\]"
