ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_table ft0 = {fb0}
dcl_function_table ft1 = {fb1}
dcl_interface fp0[1][1] = {ft0, ft1}
dcl_input_ps linear v0
dcl_input_ps linear v1.x
dcl_output o0
dcl_temps 3
fcall fp0[0][0]
mov o0, r0
ret
label fb0
mov r1.xy, this[0].yx
mul r1.z, -v1.x, cb[r1.y][r1.x].x
mul r1.z, r1.z, l(1.44269502)
exp r1.z, r1.z
add r2, v0, -cb[r1.y][r1.x + 1]
mad r0, r1.z, r2, cb[r1.y][r1.x + 1]
ret
label fb1
mov r1.xy, this[0].yx
add r1.z, v1.x, -cb[r1.y][r1.x + 1].x
div_sat r1.z, r1.z, cb[r1.y][r1.x + 1].y
add r2, -v0, cb[r1.y][r1.x]
mad r0, r1.z, r2, v0
ret
