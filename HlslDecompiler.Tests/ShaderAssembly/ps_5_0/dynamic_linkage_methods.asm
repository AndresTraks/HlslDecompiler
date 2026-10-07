ps_5_0
dcl_globalFlags refactoringAllowed
dcl_function_body fb0
dcl_function_body fb1
dcl_function_body fb2
dcl_function_body fb3
dcl_function_body fb4
dcl_function_body fb5
dcl_function_table ft0 = {fb0, fb2}
dcl_function_table ft1 = {fb1, fb3}
dcl_function_table ft2 = {fb4}
dcl_function_table ft3 = {fb5}
dcl_interface fp0[1][2] = {ft0, ft1}
dcl_interface fp1[2][1] = {ft2, ft3}
dcl_input_ps linear v0
dcl_output o0
dcl_temps 2
fcall fp0[0][0]
fcall fp0[0][1]
add r0, r0, r1
fcall fp1[1][0]
add o0, r0, r1
ret
label fb0
mov r0, l(1, 0, 0, 1)
ret
label fb1
mul r0, v0, l(0.5, 0.5, 0.5, 0.5)
ret
label fb2
add r1, v0, l(0.25, 0.25, 0.25, 0.25)
ret
label fb3
add r1, v0, l(-0.125, -0.125, -0.125, -0.125)
ret
label fb4
add r1, v0, l(0.75, 0.75, 0.75, 0.75)
ret
label fb5
mul_sat r1, v0, l(3, 3, 3, 3)
ret
