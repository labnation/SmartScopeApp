#!/bin/bash
uuid()
{
    local N B T

    for (( N=0; N < 16; ++N ))
    do
        B=$(( $RANDOM%255 ))

        if (( N == 6 ))
        then
            printf '4%X' $(( B%15 ))
        elif (( N == 8 ))
        then
            local C='89AB'
            printf '%c%X' ${C:$(( $RANDOM%${#C} )):1} $(( B%15 ))
        else
            printf '%02X' $B
        fi

        for T in 3 5 7 9
        do
            if (( T == N ))
            then
                printf '-'
                break
            fi
        done
    done

    echo
}
uuid