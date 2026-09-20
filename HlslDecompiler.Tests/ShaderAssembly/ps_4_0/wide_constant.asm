ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0
dcl_output o0
dcl_temps 1
imad r0.x, cb0[0].x, l(-1640531535), l(-1640531527)
and r0.x, r0.x, l(65535)
utof r0.x, r0.x
mul o0, r0.x, v0
ret
