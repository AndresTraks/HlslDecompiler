ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0
dcl_output o0
dcl_temps 3
fcall fp0[0][0]
mov o0, r0
ret
label fb0
mov r1.xy, this[0].yx
and r2, v0, cb[r1.y][r1.x].x
mul r0, r2, cb[r1.y][r1.x].y
ret
label fb1
mov r1.xy, this[0].yx
mul r0, v0, cb[r1.y][r1.x]
ret
