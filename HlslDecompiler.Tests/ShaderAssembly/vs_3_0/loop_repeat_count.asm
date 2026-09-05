vs_3_0
def c8, 0, 0, 0, 0
defi i0, 8, 0, 1, 0
dcl_position o0
mov r0, c8.y
loop aL, i0
add r0, r0, c0[aL]
endloop
mov o0, r0
