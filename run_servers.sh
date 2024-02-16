#!/usr/bin/env bash

set -e

export OVERMIND_NO_PORT=1
export OVERMIND_ANY_CAN_DIE=1
export OVERMIND_AUTO_RESTART=all

overmind start
