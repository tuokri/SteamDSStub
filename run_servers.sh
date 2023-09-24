#!/usr/bin/env bash

set -e

export OVERMIND_NO_PORT=1
export OVERMIND_ANY_CAN_DIE=1
export OVERMIND_AUTO_RESTART=socat1,socat2,steamds1,steamds2,a2sserver1,a2sserver2

overmind start
