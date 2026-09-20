ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
imad r0.x, cb0[0].x, l(-1640531535), l(7)
ult r0.x, r0.x, l(1073741824)
add r1, v0, v0
movc o0, r0.x, v0, r1
ret
