ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
fcall fp0[0][0]
mov o0, r0
ret
label fb0
mov r1.xy, this[0].yx
and r1.x, v1.x, cb[r1.y][r1.x].x
movc r0, r1.x, v0, l(0, 0, 0, 0)
ret
label fb1
mov r1.xy, this[0].yx
ishr r1.z, v1.x, cb[r1.y][r1.x].x
imax r1.x, r1.z, cb[r1.y][r1.x].y
itof r1.x, r1.x
mul r0, r1.x, v0
ret
